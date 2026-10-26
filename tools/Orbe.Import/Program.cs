using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Orbe.Import;
public sealed record ExportResult(string BackupPath,string JsonPath,int Appointments,int Entries);
public static class LegacyExport
{
 public static ExportResult Export(string sourcePath,string destination)
 {
  var sourceFile=Path.GetFullPath(sourcePath);var folder=Path.GetFullPath(destination);
  if(!File.Exists(sourceFile))throw new ArgumentException("Selecione um arquivo SQLite existente.");
  using var original=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=sourceFile,Mode=SqliteOpenMode.ReadOnly,Pooling=false,DefaultTimeout=10}.ToString());original.Open();
  using(var cmd=original.CreateCommand())
  {
   cmd.CommandText="SELECT name FROM sqlite_schema WHERE type='table'";using var reader=cmd.ExecuteReader();var tables=new HashSet<string>();while(reader.Read())tables.Add(reader.GetString(0));
   if(!tables.Contains("agendamentos")||!tables.Contains("lancamentos")||tables.Contains("api_keys")||tables.Contains("users")&&tables.Contains("databases"))throw new ArgumentException("Selecione apenas o banco dedicado do Orbe, nunca o catálogo ou credenciais do Nexus.");
  }
  Directory.CreateDirectory(folder);var suffix=DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff")+"-"+Guid.NewGuid().ToString("N")[..6];
  var backup=Path.Combine(folder,"orbe-backup-"+suffix+".sqlite");var output=Path.Combine(folder,"orbe-import-"+suffix+".json");
  using(var copy=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=backup,Mode=SqliteOpenMode.ReadWriteCreate,Pooling=false}.ToString())){copy.Open();original.BackupDatabase(copy);}
  using var snapshot=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=backup,Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ToString());snapshot.Open();
  var appointments=new List<object>();var entries=new List<object>();
  using(var cmd=snapshot.CreateCommand())
  {
   cmd.CommandText="SELECT id,titulo,tipo,inicio,fim,cliente,observacoes,valor_previsto_centavos FROM agendamentos ORDER BY id";
   using var r=cmd.ExecuteReader();while(r.Read())appointments.Add(new{id=r.GetString(0),title=r.GetString(1),kind=r.GetString(2),start=r.GetString(3),end=r.GetString(4),client=r.IsDBNull(5)?null:r.GetString(5),notes=r.IsDBNull(6)?null:r.GetString(6),expectedCents=r.GetInt64(7)});
  }
  using(var cmd=snapshot.CreateCommand())
  {
   cmd.CommandText="SELECT id,descricao,tipo,data,valor_centavos,agendamento_id FROM lancamentos ORDER BY id";
   using var r=cmd.ExecuteReader();while(r.Read())entries.Add(new{id=r.GetString(0),description=r.GetString(1),kind=r.GetString(2),date=r.GetString(3),cents=r.GetInt64(4),appointmentId=r.IsDBNull(5)?null:r.GetString(5)});
  }
  using(var stream=new FileStream(output,FileMode.CreateNew,FileAccess.Write,FileShare.None))JsonSerializer.Serialize(stream,new{appointments,entries},new JsonSerializerOptions{WriteIndented=true});
  return new(backup,output,appointments.Count,entries.Count);
 }
}
internal static class Command
{
 public static int Main(string[] args)
 {
  try{
   if(args.Length<2||args[0]!="--database"){Console.WriteLine("Uso: Orbe.Import --database CAMINHO_DO_BANCO [--output-dir PASTA_PRIVADA]. O original será aberto somente para leitura.");return 2;}
   var folder=args.Length==4&&args[2]=="--output-dir"?args[3]:Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Orbe","Migrations");
   var result=LegacyExport.Export(args[1],folder);Console.WriteLine($"Exportação concluída: {result.Appointments} agendamentos, {result.Entries} lançamentos.\nBackup privado: {result.BackupPath}\nArquivo para conferir no portal: {result.JsonPath}");return 0;
  }catch(Exception e) when(e is ArgumentException or SqliteException or IOException){Console.Error.WriteLine("Exportação não concluída. Confira se selecionou um banco Orbe válido, se ele está acessível e se a pasta de destino permite escrita. Nenhum registro do original foi alterado.");return 1;}
 }
}
