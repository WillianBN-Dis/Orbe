using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Agenda.Core;

public sealed record DatabaseChoice(string Id, string Nome);
public sealed record NexusColumn(string Name, string Type, bool Required, int Key, int Hidden);
public sealed record NexusSchema(NexusColumn[] Columns, string[] Identity);
public sealed record RowSnapshot(JsonElement[] Chave, JsonElement[] Valores);
public sealed record Stored<T>(T Value, RowSnapshot Snapshot);
public sealed record RecordPage(string[] Colunas, string[] Identidade, int Offset, int Limite, bool TemMais, RowSnapshot[] Registros);
public sealed class NexusException(string message, HttpStatusCode? status = null) : Exception(message)
{
    public HttpStatusCode? Status { get; } = status;
}

public sealed class NexusClient : IDisposable
{
    private readonly HttpClient http;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public NexusClient(string baseUrl, string token, HttpMessageHandler? handler = null)
    {
        if (!Uri.TryCreate(baseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var uri) ||
            uri.Scheme != "http" || uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.UserInfo.Length != 0 ||
            !(uri.Host == "localhost" || IPAddress.TryParse(uri.Host, out var ip) && IsPrivate(ip)))
            throw new ArgumentException("Use HTTP com localhost, 127.0.0.1 ou um IPv4 privado da rede local, sem caminho ou credenciais.");
        if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("Informe a chave da API Nexus.");
        http = handler is null ? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) : new HttpClient(handler);
        http.BaseAddress = uri;
        http.Timeout = TimeSpan.FromSeconds(15);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
    }
    private static bool IsPrivate(IPAddress address)
    {
        var b = address.GetAddressBytes();
        return b.Length == 4 && (b[0] == 127 || b[0] == 10 || b[0] == 172 && b[1] is >= 16 and <= 31 || b[0] == 192 && b[1] == 168);
    }
    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, object? body = null, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(method, path);
            if (body is not null) request.Content = JsonContent.Create(body);
            var response = await http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode) return response;
            var status = response.StatusCode;
            var detail = status == HttpStatusCode.BadRequest ? await ExplainBadRequest(response, ct) : null;
            response.Dispose();
            throw new NexusException(status switch
            {
                HttpStatusCode.BadRequest => detail!,
                HttpStatusCode.Unauthorized => "Chave inválida ou revogada. Confira a conexão com o Nexus.",
                HttpStatusCode.Forbidden => "A chave não permite esta operação. Confira as permissões de escrita no Nexus.",
                HttpStatusCode.NotFound => "Banco ou tabela indisponível. Confira o banco selecionado e prepare as tabelas no Nexus.",
                HttpStatusCode.Conflict => "O registro mudou ou uma restrição impediu a gravação. Atualize os dados antes de tentar novamente.",
                HttpStatusCode.TooManyRequests => "O Nexus está recebendo muitas solicitações. Aguarde e tente novamente.",
                _ => "O Nexus não concluiu a operação. Atualize os dados antes de repetir uma gravação."
            }, status);
        }
        catch (HttpRequestException) { throw new NexusException("Não foi possível acessar o Nexus. Confira o endereço, a rede e se a API está ligada. Se estava salvando, atualize antes de tentar novamente."); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw new NexusException("O Nexus demorou para responder. Se estava salvando, atualize os dados para conferir se a gravação foi concluída."); }
    }
    private static async Task<string> ExplainBadRequest(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var error = document.RootElement.TryGetProperty("erro", out var value) ? value.GetString() ?? "" : "";
            if (error.Contains("Tabela não encontrada", StringComparison.OrdinalIgnoreCase))
                return "O Nexus aceitou a chave, mas a tabela solicitada não existe neste banco. Prepare as tabelas agendamentos e lancamentos com o script do Orbe no editor SQL do Nexus.";
            if (error.Contains("Coluna de ordenação", StringComparison.OrdinalIgnoreCase))
                return "O banco foi encontrado, mas a tabela não contém a coluna id esperada pelo Orbe. Confira a estrutura no editor SQL do Nexus.";
        }
        catch (JsonException) { }
        return "O Nexus rejeitou a solicitação (HTTP 400). Confira a estrutura das tabelas e os valores informados.";
    }
    public async Task<DatabaseChoice[]> Databases(CancellationToken ct = default)
    {
        using var response = await Send(HttpMethod.Get, "api/bancos", ct: ct);
        return await response.Content.ReadFromJsonAsync<DatabaseChoice[]>(Json, ct) ?? [];
    }
    private static string Path(string db, string table) => $"api/bancos/{Uri.EscapeDataString(db)}/tabelas/{Uri.EscapeDataString(table)}/registros";
    public async Task VerifyDatabase(string db, CancellationToken ct = default)
    {
        var prefix = $"api/bancos/{Uri.EscapeDataString(db)}/tabelas";
        using var response = await Send(HttpMethod.Get, prefix, ct: ct);
        var tables = await response.Content.ReadFromJsonAsync<string[]>(Json, ct) ?? [];
        var expected = new Dictionary<string, string[]>
        {
            ["agendamentos"] = ["id", "titulo", "tipo", "inicio", "fim", "cliente", "observacoes", "valor_previsto_centavos"],
            ["lancamentos"] = ["id", "descricao", "tipo", "data", "valor_centavos", "agendamento_id"]
        };
        var missingTables = expected.Keys.Except(tables, StringComparer.Ordinal).ToArray();
        if (missingTables.Length > 0)
            throw new NexusException("A conexão com o Nexus funcionou. Faltam as tabelas: " + string.Join(", ", missingTables) + ". Abra o editor SQL desse banco no Nexus e execute cada CREATE TABLE de 001-inicial.sql separadamente. Criar somente o banco vazio ainda não prepara o Orbe.");
        foreach (var (table, columns) in expected)
        {
            using var structure = await Send(HttpMethod.Get, prefix + "/" + table + "/estrutura", ct: ct);
            var schema = await structure.Content.ReadFromJsonAsync<NexusSchema>(Json, ct) ?? throw new NexusException("Estrutura inválida recebida do Nexus.");
            var missing = columns.Except(schema.Columns.Select(c => c.Name), StringComparer.Ordinal).ToArray();
            if (missing.Length > 0) throw new NexusException($"A tabela {table} não está pronta. Faltam as colunas: {string.Join(", ", missing)}. Confira 001-inicial.sql; nenhum dado foi alterado.");
            var id = schema.Columns.Single(c => c.Name == "id");
            if (!id.Type.Contains("TEXT", StringComparison.OrdinalIgnoreCase) || schema.Identity.Length == 0)
                throw new NexusException($"A tabela {table} precisa de id TEXT PRIMARY KEY. O ID automático INTEGER do modo Visual do Nexus não é compatível. Prepare um banco dedicado pelo editor SQL com 001-inicial.sql; seus dados existentes não foram alterados.");
        }
    }
    public async Task<IReadOnlyList<Stored<T>>> Read<T>(string db, string table, Func<IReadOnlyDictionary<string, JsonElement>, T> map, CancellationToken ct = default)
    {
        var result = new List<Stored<T>>();
        for (int offset = 0; offset <= 1_000_000; offset += 200)
        {
            using var response = await Send(HttpMethod.Get, Path(db, table) + $"?offset={offset}&ordenarPor=id", ct: ct);
            var page = await response.Content.ReadFromJsonAsync<RecordPage>(Json, ct) ?? throw new NexusException("Resposta inválida do Nexus.");
            if (page.Identidade.Length == 0) throw new NexusException("Tabela sem identidade segura. Confira o script de preparação do banco.");
            foreach (var row in page.Registros)
            {
                if (row.Valores.Length != page.Colunas.Length) throw new NexusException("Resposta de tabela incompatível.");
                result.Add(new(map(page.Colunas.Select((c, i) => (c, i)).ToDictionary(p => p.c, p => row.Valores[p.i])), row));
            }
            if (!page.TemMais) return result;
            if (page.Registros.Length == 0) throw new NexusException("Paginação inválida do Nexus.");
        }
        throw new NexusException("O banco ultrapassa o limite de paginação disponível.");
    }
    public async Task Insert(string db, string table, object values, CancellationToken ct = default)
    { using var response = await Send(HttpMethod.Post, Path(db, table), new { valores = values }, ct); }
    public async Task Update(string db, string table, RowSnapshot snapshot, object values, CancellationToken ct = default)
    { using var response = await Send(HttpMethod.Patch, Path(db, table), new { chave = snapshot.Chave, anteriores = snapshot.Valores, valores = values }, ct); }
    public async Task Delete(string db, string table, RowSnapshot snapshot, CancellationToken ct = default)
    { using var response = await Send(HttpMethod.Delete, Path(db, table), new { chave = snapshot.Chave, anteriores = snapshot.Valores }, ct); }
    public void Dispose() => http.Dispose();
}

public sealed class AgendaRepository(NexusClient client, string database)
{
    public Task Verify() => client.VerifyDatabase(database);
    public Task<IReadOnlyList<Stored<Appointment>>> Appointments() => client.Read(database, "agendamentos", r => new Appointment(
        r["id"].GetString()!, r["titulo"].GetString()!, Enum.Parse<EventKind>(r["tipo"].GetString()!),
        DateTime.ParseExact(r["inicio"].GetString()!, "yyyy-MM-dd'T'HH:mm:ss", null), DateTime.ParseExact(r["fim"].GetString()!, "yyyy-MM-dd'T'HH:mm:ss", null),
        Text(r["cliente"]), Text(r["observacoes"]), r["valor_previsto_centavos"].GetInt64()));
    public Task<IReadOnlyList<Stored<MoneyEntry>>> Entries() => client.Read(database, "lancamentos", r => new MoneyEntry(
        r["id"].GetString()!, r["descricao"].GetString()!, Enum.Parse<EntryKind>(r["tipo"].GetString()!),
        DateOnly.ParseExact(r["data"].GetString()!, "yyyy-MM-dd", null), r["valor_centavos"].GetInt64(), Text(r["agendamento_id"])));
    private static string? Text(JsonElement e) => e.ValueKind == JsonValueKind.Null ? null : e.GetString();
    private static object Values(Appointment a) => new { id = a.Id, titulo = a.Title.Trim(), tipo = a.Kind.ToString(), inicio = a.Start.ToString("yyyy-MM-dd'T'HH:mm:ss"), fim = a.End.ToString("yyyy-MM-dd'T'HH:mm:ss"), cliente = a.Client, observacoes = a.Notes, valor_previsto_centavos = a.ExpectedCents };
    public Task Save(Appointment a, RowSnapshot? original = null)
    { a.Validate(); return original is null ? client.Insert(database, "agendamentos", Values(a)) : client.Update(database, "agendamentos", original, Values(a)); }
    public Task Save(MoneyEntry e)
    {
        e.Validate();
        return client.Insert(database, "lancamentos", new { id = e.Id, descricao = e.Description.Trim(), tipo = e.Kind.ToString(), data = e.Date.ToString("yyyy-MM-dd"), valor_centavos = e.Cents, agendamento_id = e.AppointmentId });
    }
    public async Task Delete(Stored<Appointment> a)
    {
        // Check the appointment before touching its finances; DELETE still checks its snapshot.
        var current = (await Appointments()).SingleOrDefault(x => x.Value.Id == a.Value.Id);
        if (current is null || JsonSerializer.Serialize(current.Snapshot) != JsonSerializer.Serialize(a.Snapshot))
            throw new NexusException("O agendamento mudou ou já foi excluído. Atualize a agenda antes de tentar novamente.", HttpStatusCode.Conflict);
        var linked = (await Entries()).Where(x => x.Value.AppointmentId == a.Value.Id).ToArray();
        try
        {
            // Keep the appointment available for recovery if one of the financial deletes fails.
            foreach (var entry in linked) await Delete(entry);
            await client.Delete(database, "agendamentos", a.Snapshot);
        }
        catch (NexusException error)
        {
            throw new NexusException("A exclusão pode ter sido concluída apenas em parte. Atualize a agenda e o financeiro para conferir antes de tentar novamente. " + error.Message, error.Status);
        }
    }
    public Task Delete(Stored<MoneyEntry> e) => client.Delete(database, "lancamentos", e.Snapshot);
}
