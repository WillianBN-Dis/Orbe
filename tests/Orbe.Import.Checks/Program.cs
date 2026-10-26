using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Orbe.Import;
var root=Path.Combine(Path.GetTempPath(),"orbe-export-check-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var checks=0;
void Check(bool value,string name){if(!value)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
try
{
 var path=Path.Combine(root,"source.sqlite");using var original=new SqliteConnection($"Data Source={path};Pooling=False");original.Open();
 using(var command=original.CreateCommand())
 {
  command.CommandText="PRAGMA journal_mode=WAL; CREATE TABLE agendamentos(id TEXT PRIMARY KEY,titulo TEXT,tipo TEXT,inicio TEXT,fim TEXT,cliente TEXT,observacoes TEXT,valor_previsto_centavos INTEGER); CREATE TABLE lancamentos(id TEXT PRIMARY KEY,descricao TEXT,tipo TEXT,data TEXT,valor_centavos INTEGER,agendamento_id TEXT); INSERT INTO agendamentos VALUES('a','Atendimento sintético','Atendimento','2026-11-02T09:00:00','2026-11-02T10:00:00',NULL,NULL,12345); INSERT INTO lancamentos VALUES('m','Recebimento','Receita','2026-11-02',12345,'a'); INSERT INTO lancamentos VALUES('n','Avulso','Despesa','2026-11-02',123,NULL)";command.ExecuteNonQuery();
 }
 byte[] HashFile(){using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);return SHA256.HashData(stream);} var before=HashFile();var result=LegacyExport.Export(path,Path.Combine(root,"private"));
 Check(before.SequenceEqual(HashFile()),"Arquivo original permanece inalterado");
 using var document=JsonDocument.Parse(File.ReadAllText(result.JsonPath));var json=document.RootElement;
 Check(result.Appointments==1&&result.Entries==2,"Backup inclui dados confirmados ainda no WAL");
 Check(json.GetProperty("appointments")[0].GetProperty("client").ValueKind==JsonValueKind.Null&&json.GetProperty("appointments")[0].GetProperty("expectedCents").GetInt64()==12345,"NULL e centavos preservados sem arredondamento");
 Check(json.GetProperty("entries")[0].GetProperty("appointmentId").GetString()=="a"&&json.GetProperty("entries")[1].GetProperty("appointmentId").ValueKind==JsonValueKind.Null,"Vínculo e lançamento avulso preservados");
 using(var copy=new SqliteConnection($"Data Source={result.BackupPath};Mode=ReadOnly;Pooling=False")){copy.Open();using var command=copy.CreateCommand();command.CommandText="SELECT COUNT(*) FROM lancamentos";Check((long)command.ExecuteScalar()! ==2,"Cópia de segurança pode ser aberta e consultada");}
 using(var command=original.CreateCommand()){command.CommandText="CREATE TABLE api_keys(hash BLOB)";command.ExecuteNonQuery();}
 try{LegacyExport.Export(path,Path.Combine(root,"forbidden"));throw new Exception("Catálogo aceito");}catch(ArgumentException){Check(!Directory.Exists(Path.Combine(root,"forbidden")),"Exportador recusa banco com tabelas de credenciais antes de copiar");}
 Console.WriteLine($"{checks} verificações de exportação/backup passaram; somente SQLite sintético.");
}
finally{if(!Path.GetFullPath(root).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(root).StartsWith("orbe-export-check-"))throw new Exception("Pasta de teste inválida");SqliteConnection.ClearAllPools();Directory.Delete(root,true);}
