using Agenda.Core;
using Nexus.Core;
using Nexus.Api;
using System.Net;
using System.Net.Sockets;

var root = Path.Combine(Path.GetTempPath(), "agenda-nexus-check-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
LocalApiHost? api = null;
int checks = 0;
void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; Console.WriteLine("PASS " + label); }
try
{
    var store = new NexusStore(root);
    store.Register("fixture", "temporary-fixture-password"); store.Login("fixture", "temporary-fixture-password");
    var bank = store.Create("Agenda de testes");
    var sql = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "schema.sql"));
    foreach (var statement in sql.Split(';', StringSplitOptions.RemoveEmptyEntries))
        if (statement.Contains("CREATE TABLE")) store.Execute(bank.Id, statement);
    var token = store.CreateApiKey("fixture-rw", true, [bank.Id]);
    var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
    api = new LocalApiHost(root); await api.ConfigureAsync(new(true, "127.0.0.1", port));
    using var client = new NexusClient(api.Address, token);
    var repo = new AgendaRepository(client, bank.Id);
    Check((await client.Databases()).Single().Id == bank.Id, "Banco via API Nexus real");
    var a = new Appointment(Guid.NewGuid().ToString("N"), "Atendimento teste", EventKind.Atendimento, new(2026,10,24,9,0,0), new(2026,10,24,10,0,0), null, null, 15000);
    await repo.Save(a);
    var snapshot = (await repo.Appointments()).Single();
    Check(snapshot.Value == a, "Inserir e ler evento real, NULL e centavos");
    await repo.Save(a with { Title = "Atualizado" }, snapshot.Snapshot);
    Check((await repo.Appointments()).Single().Value.Title == "Atualizado", "Editar evento pela API real");
    try { await repo.Save(a, snapshot.Snapshot); throw new Exception("Conflito não detectado"); }
    catch (NexusException e) { Check(e.Status == HttpStatusCode.Conflict, "Conflito otimista real"); }
    await repo.Save(new MoneyEntry(Guid.NewGuid().ToString("N"), "Recebimento", EntryKind.Receita, new(2026,10,24), 15000, a.Id));
    await repo.Save(new MoneyEntry(Guid.NewGuid().ToString("N"), "Despesa", EntryKind.Despesa, new(2026,10,24), 3000, null));
    Check(Finance.Summarize((await repo.Entries()).Select(e=>e.Value), new(2026,10,24), Period.Dia).Profit == 12000, "Financeiro com dados reais da API");
    await repo.Delete((await repo.Appointments()).Single());
    Check((await repo.Appointments()).Count == 0 && (await repo.Entries()).Count == 2, "Excluir evento preserva financeiro");
    var entry = (await repo.Entries()).First(); await repo.Delete(entry);
    Check((await repo.Entries()).Count == 1, "Excluir lançamento pela API real");
    var readKey = store.CreateApiKey("fixture-ro", false, [bank.Id]);
    using var readClient = new NexusClient(api.Address, readKey);
    try { await new AgendaRepository(readClient, bank.Id).Save(a); throw new Exception("Escrita permitida"); }
    catch (NexusException e) { Check(e.Status == HttpStatusCode.Forbidden, "Chave somente leitura não grava"); }
    Console.WriteLine($"{checks} verificações reais passaram. Dados exclusivamente temporários.");
}
finally
{
    if (api is not null) await api.DisposeAsync();
    if (!root.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase)) throw new Exception("Diretório de teste inválido");
    Directory.Delete(root, true);
}
