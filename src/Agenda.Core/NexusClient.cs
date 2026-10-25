using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Agenda.Core;

public sealed record DatabaseChoice(string Id, string Nome);
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
            response.Dispose();
            throw new NexusException(status switch
            {
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
    public async Task<DatabaseChoice[]> Databases(CancellationToken ct = default)
    {
        using var response = await Send(HttpMethod.Get, "api/bancos", ct: ct);
        return await response.Content.ReadFromJsonAsync<DatabaseChoice[]>(Json, ct) ?? [];
    }
    private static string Path(string db, string table) => $"api/bancos/{Uri.EscapeDataString(db)}/tabelas/{Uri.EscapeDataString(table)}/registros";
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
    public Task Delete(Stored<Appointment> a) => client.Delete(database, "agendamentos", a.Snapshot);
    public Task Delete(Stored<MoneyEntry> e) => client.Delete(database, "lancamentos", e.Snapshot);
}
