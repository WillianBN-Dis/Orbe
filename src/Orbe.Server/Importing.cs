using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Orbe.Server;
public sealed record LegacyAppointment(string Id,string Title,string Kind,string Start,string End,string? Client,string? Notes,long ExpectedCents);
public sealed record LegacyEntry(string Id,string Description,string Kind,string Date,long Cents,string? AppointmentId);
public sealed record ImportInput(LegacyAppointment[] Appointments,LegacyEntry[] Entries);
public sealed record AssignInput(string EmployeeId,Guid Version);
public sealed class Importing(OrbeDb db,TimeProvider clock)
{
 private static string Hash(ImportInput input)=>Secrets.Hash(JsonSerializer.Serialize(new{appointments=input.Appointments.OrderBy(x=>x.Id),entries=input.Entries.OrderBy(x=>x.Id)}));
 private static void Text(string? value,int max){if(string.IsNullOrWhiteSpace(value)||value.Length>max)throw new ApiError(400,"Há campos obrigatórios vazios ou extensos demais no arquivo.");}
 private static DateTimeOffset Instant(string value,TimeZoneInfo zone)
 {
  if(!DateTime.TryParseExact(value,"yyyy-MM-dd'T'HH:mm:ss",CultureInfo.InvariantCulture,DateTimeStyles.None,out var local)||zone.IsInvalidTime(local)||zone.IsAmbiguousTime(local))throw new ApiError(400,"Há uma data inválida ou ambígua. Confira os horários do arquivo antes de importar.");
  return new(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local,DateTimeKind.Unspecified),zone));
 }
 private static void Validate(ImportInput input,TimeZoneInfo zone)
 {
  if(input.Appointments is null||input.Entries is null||input.Appointments.Length>10000||input.Entries.Length>10000||input.Appointments.Any(x=>x is null)||input.Entries.Any(x=>x is null))throw new ApiError(400,"Arquivo de importação inválido ou extenso demais.");
  if(input.Appointments.Select(x=>x.Id).Distinct().Count()!=input.Appointments.Length||input.Entries.Select(x=>x.Id).Distinct().Count()!=input.Entries.Length)throw new ApiError(400,"O arquivo contém identificadores repetidos.");
  foreach(var item in input.Appointments)
  {Text(item.Id,128);Text(item.Title,200);if(item.Kind is not ("Atendimento" or "Folga" or "Compromisso" or "Outro")||item.ExpectedCents<0||item.Client?.Length>150||item.Notes?.Length>2000||Instant(item.End,zone)<=Instant(item.Start,zone))throw new ApiError(400,"Agendamento inválido no arquivo.");}
  foreach(var item in input.Entries)
  {Text(item.Id,128);Text(item.Description,200);if(item.Kind is not ("Receita" or "Despesa")||item.Cents<=0||!DateOnly.TryParseExact(item.Date,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _)||item.AppointmentId?.Length>128)throw new ApiError(400,"Lançamento inválido no arquivo.");}
 }
 public async Task<object> Preview(ImportInput input)
 {
  var settings=await db.Settings.SingleAsync();Validate(input,TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZoneId));var hash=Hash(input);
  return new{hash,appointments=input.Appointments.Length,entries=input.Entries.Length,income=input.Entries.Where(x=>x.Kind=="Receita").Sum(x=>x.Cents),expense=input.Entries.Where(x=>x.Kind=="Despesa").Sum(x=>x.Cents),orphanLinks=input.Entries.Count(x=>x.AppointmentId is not null&&!input.Appointments.Any(a=>a.Id==x.AppointmentId)),alreadyImported=await db.Imports.AnyAsync(x=>x.Hash==hash)};
 }
 public async Task<object> Commit(ImportInput input)
 {
  await using var tx=await db.Database.BeginTransactionAsync();
  await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(582106)");
  await db.Database.ExecuteSqlRawAsync("SELECT 1 FROM \"Settings\" WHERE \"Id\"=1 FOR UPDATE");
  var settings=await db.Settings.SingleAsync();var zone=TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZoneId);Validate(input,zone);var hash=Hash(input);
  if(await db.Imports.AnyAsync(x=>x.Hash==hash))return new{hash,alreadyImported=true};
  if(settings.PublicBookingOpen)throw new ApiError(409,"Feche as reservas públicas antes de importar dados.");
  var appointmentIds=input.Appointments.Select(x=>x.Id).ToArray();var entryIds=input.Entries.Select(x=>x.Id).ToArray();
  if(await db.Appointments.AnyAsync(x=>appointmentIds.Contains(x.Id))||await db.Entries.AnyAsync(x=>entryIds.Contains(x.Id)))throw new ApiError(409,"Há IDs já existentes na nuvem. Nenhum dado foi sobrescrito.");
  var known=appointmentIds.ToHashSet(StringComparer.Ordinal);
  db.Appointments.AddRange(input.Appointments.Select(x=>new ScheduleItem{Id=x.Id,Title=x.Title,Kind=x.Kind,Start=Instant(x.Start,zone),End=Instant(x.End,zone),LegacyClient=x.Client,Notes=x.Notes,ExpectedCents=x.ExpectedCents,DurationMinutes=checked((int)(Instant(x.End,zone)-Instant(x.Start,zone)).TotalMinutes)}));
  db.Entries.AddRange(input.Entries.Select(x=>new FinancialEntry{Id=x.Id,Description=x.Description,Kind=x.Kind,Date=DateOnly.ParseExact(x.Date,"yyyy-MM-dd",CultureInfo.InvariantCulture),Cents=x.Cents,AppointmentId=x.AppointmentId is not null&&known.Contains(x.AppointmentId)?x.AppointmentId:null,LegacyAppointmentId=x.AppointmentId is not null&&!known.Contains(x.AppointmentId)?x.AppointmentId:null}));
  db.Imports.Add(new(){Hash=hash,ImportedAt=clock.GetUtcNow(),Appointments=input.Appointments.Length,Entries=input.Entries.Length});settings.ImportCompleted=false;settings.Version=Guid.NewGuid();await db.SaveChangesAsync();await tx.CommitAsync();return new{hash,alreadyImported=false};
 }
 public async Task AssignAppointment(string id,AssignInput input)
 {
  await using var tx=await db.Database.BeginTransactionAsync();await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Employees\" WHERE \"Id\"={input.EmployeeId} FOR UPDATE");
  if(!await db.Employees.AnyAsync(x=>x.Id==input.EmployeeId&&x.Enabled))throw new ApiError(400,"Selecione um profissional ativo.");
  var item=await db.Appointments.SingleOrDefaultAsync(x=>x.Id==id)??throw new ApiError(404,"Agendamento não encontrado.");if(item.EmployeeId is not null||item.Version!=input.Version)throw new ApiError(409,"Este registro já foi atribuído ou alterado.");
  item.EmployeeId=input.EmployeeId;item.Version=Guid.NewGuid();
  await db.Entries.Where(x=>x.AppointmentId==id).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.EmployeeId,input.EmployeeId).SetProperty(x=>x.Version,Guid.NewGuid()));
  await db.SaveChangesAsync();await tx.CommitAsync();
 }
}
public static class ImportRoutes
{
 public static void MapImports(this WebApplication app)
 {
  var group=app.MapGroup("/api/v1/owner/import").RequireAuthorization(p=>p.RequireRole(Roles.Owner));
  group.MapPost("/preview",async(ImportInput input,Importing service)=>Results.Ok(await service.Preview(input)));
  group.MapPost("/commit",async(ImportInput input,Importing service)=>Results.Ok(await service.Commit(input)));
  group.MapGet("/unassigned",async(OrbeDb db)=>Results.Ok(new{appointments=await db.Appointments.Where(x=>x.EmployeeId==null).OrderBy(x=>x.Start).Take(500).ToArrayAsync(),entries=await db.Entries.Where(x=>x.EmployeeId==null&&x.AppointmentId==null).OrderBy(x=>x.Date).Take(500).ToArrayAsync()}));
  group.MapPost("/appointments/{id}/assign",async(string id,AssignInput input,Importing service)=>{await service.AssignAppointment(id,input);return Results.NoContent();});
  group.MapPost("/entries/{id}/assign",async(string id,AssignInput input,OrbeDb db)=>{var item=await db.Entries.SingleOrDefaultAsync(x=>x.Id==id)??throw new ApiError(404,"Lançamento não encontrado.");if(item.EmployeeId is not null||item.AppointmentId is not null||item.Version!=input.Version)throw new ApiError(409,"Registro já atribuído ou alterado.");if(!await db.Employees.AnyAsync(x=>x.Id==input.EmployeeId&&x.Enabled))throw new ApiError(400,"Profissional inválido.");item.EmployeeId=input.EmployeeId;item.Version=Guid.NewGuid();await db.SaveChangesAsync();return Results.NoContent();});
 }
}
