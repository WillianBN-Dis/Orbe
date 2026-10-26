using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Orbe.Cloud.Core;

namespace Orbe.Server;

public sealed record HoursInput(Guid Version,WeeklyWindow[] Weekly,DateSchedule[] Exceptions);
public sealed record ReservationInput(string EmployeeId,string ServiceId,DateTimeOffset Start);
public sealed record MoveInput(DateTimeOffset Start,Guid Version);
public sealed record VersionInput(Guid Version);
public sealed record BlockInput(string? EmployeeId,string Title,string Kind,DateTimeOffset Start,DateTimeOffset End,string? Notes,Guid? Version=null,string? Id=null);
public sealed record TeamAppointmentInput(string? EmployeeId,string Title,DateTimeOffset Start,DateTimeOffset End,string? ClientId,string? LegacyClient,string? Notes,long ExpectedCents,Guid? Version,string? Id=null);
public sealed record MoneyInput(string? EmployeeId,string? AppointmentId,string Description,string Kind,DateOnly Date,long Cents,string? Id=null);

public sealed class Scheduling(OrbeDb db,Actor actor,TimeProvider clock)
{
 private async Task Lock(string employee)=>await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Employees\" WHERE \"Id\"={employee} FOR UPDATE");
 public async Task<BusinessSettings> Settings()=>await db.Settings.SingleAsync();
 private static void Version(Guid expected,Guid actual){if(expected!=actual)throw new ApiError(409,"Os dados mudaram. Atualize antes de continuar.");}
 private async Task<(WeeklyWindow[] Week,DateSchedule[] Days)> Hours(string employee)
 {
  var weekly=await db.WeekWindows.Where(x=>x.EmployeeId==employee).ToArrayAsync();
  var days=await db.DayOverrides.Where(x=>x.EmployeeId==employee).ToArrayAsync();
  return(weekly.Select(x=>new WeeklyWindow((DayOfWeek)x.Day,new(x.StartMinute,x.EndMinute))).ToArray(),days.Select(x=>new DateSchedule(x.Date,JsonSerializer.Deserialize<WorkingWindow[]>(x.WindowsJson)!)).ToArray());
 }
 public async Task<object> ReadHours(string? employee)
 {
  var id=await actor.Employee(employee);var person=await db.Employees.SingleAsync(x=>x.Id==id);var hours=await Hours(id);
  return new{employeeId=id,person.Version,weekly=hours.Week,exceptions=hours.Days};
 }
 private static bool Fits(DateTimeOffset start,DateTimeOffset end,BookingPolicy policy,WeeklyWindow[] week,DateSchedule[] days)
 {
  var zone=TimeZoneInfo.FindSystemTimeZoneById(policy.TimeZoneId);
  var local=TimeZoneInfo.ConvertTime(start,zone).DateTime;var until=TimeZoneInfo.ConvertTime(end,zone).DateTime;
  var date=DateOnly.FromDateTime(local);var windows=days.SingleOrDefault(x=>x.Date==date)?.Windows??week.Where(x=>x.Day==date.DayOfWeek).Select(x=>x.Window).ToArray();
  return windows.Any(x=>local>=date.ToDateTime(TimeOnly.MinValue).AddMinutes(x.StartMinute)&&until<=date.ToDateTime(TimeOnly.MinValue).AddMinutes(x.EndMinute));
 }
 private static void ValidateHours(HoursInput input)
 {
  if(input.Weekly is null||input.Exceptions is null||input.Weekly.Length>70||input.Exceptions.Length>366)throw new ApiError(400,"Jornada inválida ou extensa demais.");
  if(input.Exceptions.Any(x=>x is null||x.Windows is null||x.Windows.Any(w=>w is null)))throw new ApiError(400,"Jornada especial inválida.");
  if(input.Exceptions.Select(x=>x.Date).Distinct().Count()!=input.Exceptions.Length)throw new ApiError(400,"Há datas especiais repetidas.");
  foreach(var row in input.Weekly)if(row is null||row.Window is null||!Enum.IsDefined(row.Day))throw new ApiError(400,"Dia da semana ou período inválido.");
  foreach(var group in input.Weekly.GroupBy(x=>x.Day).Select(x=>x.Select(y=>y.Window)).Concat(input.Exceptions.Select(x=>(IEnumerable<WorkingWindow>)x.Windows)))
  {
   var rows=group.OrderBy(x=>x.StartMinute).ToArray();if(rows.Length>10)throw new ApiError(400,"Use até 10 períodos por dia.");
   foreach(var row in rows)row.Validate();for(int i=1;i<rows.Length;i++)if(rows[i].StartMinute<rows[i-1].EndMinute)throw new ApiError(400,"Períodos de trabalho sobrepostos.");
  }
 }
 public async Task SaveHours(string? employee,HoursInput input)
 {
  var id=await actor.Employee(employee);ValidateHours(input);
  await using var tx=await db.Database.BeginTransactionAsync();await Lock(id);
  var person=await db.Employees.SingleAsync(x=>x.Id==id);Version(input.Version,person.Version);
  var policy=(await Settings()).Policy();var now=clock.GetUtcNow();
  var future=await db.Appointments.Where(x=>x.EmployeeId==id&&x.Status=="Confirmed"&&x.Kind=="Atendimento"&&x.End>now).ToArrayAsync();
  var conflicts=future.Where(x=>!Fits(x.Start,x.End,policy,input.Weekly,input.Exceptions)).Select(x=>new{x.Id,x.Title,x.Start,x.End}).ToArray();
  if(conflicts.Length>0)throw new ApiError(409,"Remarque ou cancele os atendimentos antes de alterar esta jornada.",conflicts);
  await db.WeekWindows.Where(x=>x.EmployeeId==id).ExecuteDeleteAsync();await db.DayOverrides.Where(x=>x.EmployeeId==id).ExecuteDeleteAsync();
  db.WeekWindows.AddRange(input.Weekly.Select(x=>new WeekWindow{EmployeeId=id,Day=(int)x.Day,StartMinute=x.Window.StartMinute,EndMinute=x.Window.EndMinute}));
  db.DayOverrides.AddRange(input.Exceptions.Select(x=>new DayOverride{EmployeeId=id,Date=x.Date,WindowsJson=JsonSerializer.Serialize(x.Windows)}));
  person.Version=Guid.NewGuid();await db.SaveChangesAsync();await tx.CommitAsync();
 }
 public async Task<object> AvailabilityFor(string employee,string service,DateOnly from,int days)
 {
  if(days is <1 or >31)throw new ApiError(400,"Consulte até 31 dias por vez.");
  var settings=await Settings();if(!settings.PublicBookingOpen)return new{open=false,days=Array.Empty<object>()};
  var offered=await Offer(employee,service);var hours=await Hours(employee);
  var start=from.ToDateTime(TimeOnly.MinValue,DateTimeKind.Utc).AddDays(-1);var end=start.AddDays(days+2);
  var busy=await db.Appointments.Where(x=>x.EmployeeId==employee&&x.Status=="Confirmed"&&x.Start<end&&x.End>start).Select(x=>new OccupiedInterval(x.Start,x.End)).ToArrayAsync();
  return new{open=true,days=Enumerable.Range(0,days).Select(i=>new{date=from.AddDays(i),slots=Availability.Calculate(from.AddDays(i),offered.DurationMinutes,settings.Policy(),hours.Week,hours.Days,busy,clock.GetUtcNow())}).ToArray()};
 }
 public async Task<object> AvailabilityForBooking(string id,DateOnly from,int days)
 {
  if(days is <1 or >31)throw new ApiError(400,"Consulte até 31 dias por vez.");
  var item=await Accessible(id);if(item.Status!="Confirmed"||item.EmployeeId is null||item.ServiceId is null)throw new ApiError(409,"Este agendamento precisa ser ajustado pela equipe.");
  var settings=await Settings();if(!settings.PublicBookingOpen)return new{open=false,days=Array.Empty<object>()};
  await Offer(item.EmployeeId,item.ServiceId);var hours=await Hours(item.EmployeeId);
  var start=from.ToDateTime(TimeOnly.MinValue,DateTimeKind.Utc).AddDays(-1);var end=start.AddDays(days+2);
  var busy=await db.Appointments.Where(x=>x.Id!=id&&x.EmployeeId==item.EmployeeId&&x.Status=="Confirmed"&&x.Start<end&&x.End>start).Select(x=>new OccupiedInterval(x.Start,x.End)).ToArrayAsync();
  return new{open=true,days=Enumerable.Range(0,days).Select(i=>new{date=from.AddDays(i),slots=Availability.Calculate(from.AddDays(i),item.DurationMinutes,settings.Policy(),hours.Week,hours.Days,busy,clock.GetUtcNow())}).ToArray()};
 }
 private async Task<Service> Offer(string employee,string service)
 {
  if(!await db.Employees.AnyAsync(x=>x.Id==employee&&x.Enabled&&x.User.Active)||!await db.EmployeeServices.AnyAsync(x=>x.EmployeeId==employee&&x.ServiceId==service))throw new ApiError(404,"Profissional ou serviço indisponível.");
  return await db.Services.SingleOrDefaultAsync(x=>x.Id==service&&x.Enabled)??throw new ApiError(404,"Serviço indisponível.");
 }
 private async Task CheckSlot(string employee,Service service,DateTimeOffset start,string? exclude=null)
 {
  var policy=(await Settings()).Policy();var hours=await Hours(employee);var zone=TimeZoneInfo.FindSystemTimeZoneById(policy.TimeZoneId);
  var date=DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(start,zone).DateTime);
  var end=start.AddMinutes(service.DurationMinutes);
  var busy=await db.Appointments.Where(x=>x.EmployeeId==employee&&x.Status=="Confirmed"&&x.Id!=exclude&&x.Start<end&&x.End>start).Select(x=>new OccupiedInterval(x.Start,x.End)).ToArrayAsync();
  if(!Availability.Calculate(date,service.DurationMinutes,policy,hours.Week,hours.Days,busy,clock.GetUtcNow()).Any(x=>x.Start==start))throw new ApiError(409,"Este horário não está mais disponível. Escolha outro horário.");
 }
 public async Task<ScheduleItem> Reserve(ReservationInput input,string key)
 {
  if(!actor.Client)throw new ApiError(403,"Entre com sua conta de cliente para reservar pelo site.");
  if(string.IsNullOrWhiteSpace(key)||key.Length>128)throw new ApiError(400,"Identificador de confirmação inválido.");
  var client=actor.Id;var start=input.Start.ToUniversalTime();var hash=Secrets.Hash(JsonSerializer.Serialize(new{input.EmployeeId,input.ServiceId,start}));
  await using var tx=await db.Database.BeginTransactionAsync();
  // The client lock also serializes identical keys sent to different employees.
  await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"AspNetUsers\" WHERE \"Id\"={client} FOR UPDATE");await Lock(input.EmployeeId);
  var prior=await db.BookingRequests.SingleOrDefaultAsync(x=>x.UserId==client&&x.Key==key);
  if(prior is not null)
  {if(prior.RequestHash!=hash)throw new ApiError(409,"Esta confirmação já foi usada para outra reserva.");return await db.Appointments.SingleOrDefaultAsync(x=>x.Id==prior.AppointmentId)??throw new ApiError(410,"Esta reserva já foi excluída.");}
  if(!(await Settings()).PublicBookingOpen)throw new ApiError(409,"As reservas online ainda não estão abertas.");
  var service=await Offer(input.EmployeeId,input.ServiceId);await CheckSlot(input.EmployeeId,service,start);
  var end=start.AddMinutes(service.DurationMinutes);
  if(await db.Appointments.AnyAsync(x=>x.ClientId==client&&x.Status=="Confirmed"&&x.Start<end&&x.End>start))throw new ApiError(409,"Você já tem um atendimento neste período.");
  var result=new ScheduleItem{EmployeeId=input.EmployeeId,ClientId=client,ServiceId=service.Id,Title=service.Name,Start=start,End=end,ExpectedCents=service.PriceCents,DurationMinutes=service.DurationMinutes};
  db.Appointments.Add(result);db.BookingRequests.Add(new(){UserId=client,Key=key,RequestHash=hash,AppointmentId=result.Id});await db.SaveChangesAsync();await tx.CommitAsync();return result;
 }
 public async Task<ScheduleItem> Accessible(string id,bool teamOnly=false)
 {
  var item=await db.Appointments.SingleOrDefaultAsync(x=>x.Id==id)??throw new ApiError(404,"Agendamento não encontrado.");
  if(actor.Client&&!teamOnly){if(item.ClientId!=actor.Id)throw new ApiError(404,"Agendamento não encontrado.");}
  else{actor.RequireTeam();if(!actor.Owner)await actor.Employee(item.EmployeeId??"unassigned");}
  return item;
 }
 public async Task Cancel(string id,VersionInput input)
 {
  var item=await Accessible(id);await using var tx=await db.Database.BeginTransactionAsync();
  if(item.EmployeeId is not null)await Lock(item.EmployeeId);await db.Entry(item).ReloadAsync();Version(input.Version,item.Version);
  if(item.Status!="Confirmed")throw new ApiError(409,"Este agendamento já foi cancelado.");
  if(actor.Client&&!Availability.CanClientChange(item.Start,clock.GetUtcNow(),(await Settings()).Policy()))throw new ApiError(409,"O prazo para alteração online terminou. Entre em contato com a equipe.");
  item.Status="Cancelled";item.Version=Guid.NewGuid();await db.SaveChangesAsync();await tx.CommitAsync();
 }
 public async Task<ScheduleItem> Move(string id,MoveInput input)
 {
  var item=await Accessible(id);if(item.EmployeeId is null||item.ServiceId is null)throw new ApiError(409,"A equipe precisa ajustar este agendamento legado.");
  await using var tx=await db.Database.BeginTransactionAsync();
  if(item.ClientId is not null)await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"AspNetUsers\" WHERE \"Id\"={item.ClientId} FOR UPDATE");
  await Lock(item.EmployeeId);await db.Entry(item).ReloadAsync();Version(input.Version,item.Version);
  if(item.Status!="Confirmed")throw new ApiError(409,"Agendamento cancelado não pode ser remarcado.");
  if(actor.Client&&!Availability.CanClientChange(item.Start,clock.GetUtcNow(),(await Settings()).Policy()))throw new ApiError(409,"O prazo para remarcação terminou. Entre em contato com a equipe.");
  var service=await Offer(item.EmployeeId,item.ServiceId);
  // Existing duration and price remain snapshots of the original reservation.
  var snapshot=new Service{Id=service.Id,DurationMinutes=item.DurationMinutes,PriceCents=item.ExpectedCents};
  var start=input.Start.ToUniversalTime();var end=start.AddMinutes(item.DurationMinutes);await CheckSlot(item.EmployeeId,snapshot,start,item.Id);
  if(item.ClientId is not null&&await db.Appointments.AnyAsync(x=>x.Id!=id&&x.ClientId==item.ClientId&&x.Status=="Confirmed"&&x.Start<end&&x.End>start))throw new ApiError(409,"Já existe outro atendimento da cliente neste período.");
  item.Start=start;item.End=end;item.Version=Guid.NewGuid();await db.SaveChangesAsync();await tx.CommitAsync();return item;
 }
 public async Task Delete(string id,VersionInput input)
 {
  var item=await Accessible(id,true);await using var tx=await db.Database.BeginTransactionAsync();if(item.EmployeeId is not null)await Lock(item.EmployeeId);
  await db.Entry(item).ReloadAsync();Version(input.Version,item.Version);db.Appointments.Remove(item);await db.SaveChangesAsync();await tx.CommitAsync();
 }
 public async Task<ScheduleItem> Block(BlockInput input)
 {
  var employee=await actor.Employee(input.EmployeeId);
  if(string.IsNullOrWhiteSpace(input.Title)||input.Title.Length>200||input.Kind is not ("Folga" or "Compromisso" or "Outro")||input.End<=input.Start||input.End-input.Start>TimeSpan.FromDays(366)||input.Notes?.Length>2000)throw new ApiError(400,"Confira o título e o período do bloqueio.");
  if(input.Id is not null&&!Guid.TryParse(input.Id,out _))throw new ApiError(400,"Identificador de confirmação inválido.");
  await using var tx=await db.Database.BeginTransactionAsync();await Lock(employee);
  var start=input.Start.ToUniversalTime();var end=input.End.ToUniversalTime();
  if(input.Id is not null&&await db.Appointments.SingleOrDefaultAsync(x=>x.Id==input.Id) is {} prior)
  {if(prior.EmployeeId!=employee||prior.Kind!=input.Kind||prior.Title!=input.Title.Trim()||prior.Start!=start||prior.End!=end||prior.Notes!=input.Notes||prior.Status!="Confirmed")throw new ApiError(409,"Esta confirmação já foi usada para outro bloqueio.");return prior;}
  var conflicts=await db.Appointments.Where(x=>x.EmployeeId==employee&&x.Status=="Confirmed"&&x.Start<end&&x.End>start).Select(x=>new{x.Id,x.Title,x.Start,x.End}).ToArrayAsync();
  if(conflicts.Length>0)throw new ApiError(409,"Este período já está ocupado. Resolva os agendamentos ou bloqueios existentes primeiro.",conflicts);
  var item=new ScheduleItem{EmployeeId=employee,Title=input.Title.Trim(),Kind=input.Kind,Start=start,End=end,Notes=input.Notes};if(input.Id is not null)item.Id=input.Id;db.Appointments.Add(item);await db.SaveChangesAsync();await tx.CommitAsync();return item;
 }
 public async Task<ScheduleItem> EditBlock(string id,BlockInput input)
 {
  var employee=await actor.Employee(input.EmployeeId);var item=await Accessible(id,true);
  if(item.EmployeeId!=employee||item.Kind=="Atendimento"||input.Kind!=item.Kind||input.Version is null||string.IsNullOrWhiteSpace(input.Title)||input.Title.Length>200||input.End<=input.Start||input.End-input.Start>TimeSpan.FromDays(366)||input.Notes?.Length>2000)throw new ApiError(400,"Confira o bloqueio. Profissional e tipo são preservados na edição.");
  await using var tx=await db.Database.BeginTransactionAsync();await Lock(employee);await db.Entry(item).ReloadAsync();Version(input.Version.Value,item.Version);
  item.Title=input.Title.Trim();item.Start=input.Start.ToUniversalTime();item.End=input.End.ToUniversalTime();item.Notes=input.Notes;item.Version=Guid.NewGuid();await db.SaveChangesAsync();await tx.CommitAsync();return item;
 }
}
