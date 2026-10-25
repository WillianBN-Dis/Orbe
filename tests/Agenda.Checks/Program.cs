using Agenda.Core;
using System.Net;
using System.Text;
using System.Text.Json;

var checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
void Reject(Action action, string name) { try { action(); } catch (ArgumentException) { Check(true, name); return; } throw new Exception(name); }
var day = new DateOnly(2026, 10, 24);
var money = new[] {
    new MoneyEntry("1", "Serviço", EntryKind.Receita, day, 25000, "a"),
    new MoneyEntry("2", "Material", EntryKind.Despesa, day, 7000, null),
    new MoneyEntry("3", "Outro dia", EntryKind.Receita, day.AddDays(-1), 10000, null),
    new MoneyEntry("4", "Outro ano", EntryKind.Receita, new(2025, 10, 24), 99999, null),
    new MoneyEntry("5", "Outro mês", EntryKind.Despesa, new(2026, 9, 1), 2000, null) };
var daily = Finance.Summarize(money, day, Period.Dia);
Check(daily.Income == 25000 && daily.Expense == 7000 && daily.Profit == 18000, "Resultado diário efetivo");
var monthly = Finance.Summarize(money, day, Period.Mes);
Check(monthly.Profit == 28000 && monthly.Buckets.Count == 31, "Mês e seus dias");
var yearly = Finance.Summarize(money, day, Period.Ano);
Check(yearly.Profit == 26000 && yearly.Buckets.Count == 12 && yearly.Buckets[8].Profit == -2000, "Ano, meses e prejuízo");
Check(Finance.Summarize([], new(2024, 2, 1), Period.Mes).Buckets.Count == 29, "Fevereiro bissexto e período vazio");
Check(Finance.ParseCents("1.234,56") == 123456, "Moeda brasileira sem perda de precisão");
Reject(() => Finance.ParseCents("1,234"), "Rejeita fração de centavo");
Reject(() => new MoneyEntry("x", "", EntryKind.Receita, day, 1, null).Validate(), "Descrição obrigatória");
Reject(() => new MoneyEntry("x", "Teste", EntryKind.Despesa, day, 0, null).Validate(), "Valor positivo");
var a = new Appointment("a", "Plantão", EventKind.Compromisso, new(2026, 10, 24, 23, 0, 0), new(2026, 10, 25, 0, 0, 0), null, null, 20000);
a.Validate();
Check(a.Intersects(day) && !a.Intersects(day.AddDays(1)), "Evento termina à meia-noite sem invadir próximo dia");
Check((a with { End = new(2026, 10, 25, 1, 0, 0) }).Intersects(day.AddDays(1)), "Evento atravessa dias");
Reject(() => (a with { End = a.Start }).Validate(), "Horário inválido");
Reject(() => new NexusClient("http://8.8.8.8:5080", "test"), "Rejeita servidor fora da rede privada");
Reject(() => new NexusClient("http://192.168.1.2:5080/path", "test"), "Rejeita caminho na URL base");
var handler = new FakeHandler();
using var client = new NexusClient("http://127.0.0.1:5080", "test-secret", handler);
var repo = new AgendaRepository(client, "test-bank");
Check((await client.Databases()).Single().Id == "test-bank", "Lista de bancos autorizados");
var appointments = await repo.Appointments();
Check(appointments.Count == 201 && handler.ReadPages == 2, "Paginação além de 200 linhas");
Check(appointments[0].Value.Client is null && appointments[0].Snapshot.Valores.Length == 9, "NULL e snapshot completo preservados");
await repo.Delete(appointments[0]);
Check(handler.LastMethod == HttpMethod.Delete && handler.LastBody!.Contains("anteriores") && handler.LastBody.Contains("chave"), "DELETE com snapshot otimista");
await repo.Save(appointments[0].Value with { Title = "Alterado" }, appointments[0].Snapshot);
Check(handler.LastMethod == HttpMethod.Patch && handler.LastBody!.Contains("Alterado"), "PATCH com dados e snapshot");
await repo.Save(money[0]);
Check(handler.LastMethod == HttpMethod.Post && handler.LastBody!.Contains("25000"), "POST em centavos");
foreach (var status in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.Conflict, HttpStatusCode.TooManyRequests, HttpStatusCode.ServiceUnavailable })
{
    handler.Failure = status;
    try { await repo.Save(money[0]); throw new Exception("Status ignorado"); }
    catch (NexusException e) { Check(e.Status == status && !e.Message.Contains("test-secret"), "Tratamento sem segredos: " + status); }
}
Check(handler.WriteCount == 8, "Nenhuma repetição automática de escrita");
Console.WriteLine($"{checks} verificações passaram.");

sealed class FakeHandler : HttpMessageHandler
{
    public int ReadPages, WriteCount;
    public HttpMethod? LastMethod;
    public string? LastBody;
    public HttpStatusCode? Failure;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Headers.Authorization?.ToString() != "Bearer test-secret") throw new Exception("Bearer ausente");
        LastMethod = request.Method;
        if (request.Method != HttpMethod.Get)
        {
            WriteCount++; LastBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return Response(Failure ?? HttpStatusCode.OK, "{\"sucesso\":true}");
        }
        if (request.RequestUri!.AbsolutePath == "/api/bancos") return Response(HttpStatusCode.OK, "[{\"id\":\"test-bank\",\"nome\":\"Teste\"}]");
        ReadPages++;
        var first = request.RequestUri.Query.Contains("offset=0&");
        var rows = Enumerable.Range(first ? 0 : 200, first ? 200 : 1).Select(i => new {
            chave = new[] { i + 1 }, valores = new object?[] { "event-" + i, "Evento", "Compromisso", "2026-10-24T09:00:00", "2026-10-24T10:00:00", null, null, 0, "extra" }
        });
        return Response(HttpStatusCode.OK, JsonSerializer.Serialize(new { colunas = new[] { "id", "titulo", "tipo", "inicio", "fim", "cliente", "observacoes", "valor_previsto_centavos", "campo_extra" }, identidade = new[] { "rowid" }, offset = first ? 0 : 200, limite = 200, temMais = first, registros = rows }));
    }
    static HttpResponseMessage Response(HttpStatusCode code, string json) => new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}
