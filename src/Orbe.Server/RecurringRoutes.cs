using Microsoft.EntityFrameworkCore;
using Orbe.Cloud.Core;

namespace Orbe.Server;

public sealed record RecurringInput(Guid SeriesId,string? EmployeeId,string Title,string Kind,string? ClientUsername,string? Notes,
 DateOnly FirstDate,TimeOnly StartTime,TimeOnly EndTime,int Count,long ExpectedCents,
 int? EveryWeeks=null,int? EveryDays=null,bool NotifyUnavailable=false);

public static class RecurringRoutes
{
 public static void MapRecurring(this RouteGroupBuilder team)
 {
  team.MapGet("/recurring/client",async(string username,string? employeeId,Actor actor,OrbeDb db)=>{
   await actor.Employee(employeeId);
   var client=await FindClient(db,username);
   return Results.Ok(new{client.FullName});
  }).RequireRateLimiting("auth");
  team.MapPost("/recurring",Create);
 }

 internal static string? NormalizeUsername(string? value)
 {
  var name=value?.Trim().ToLowerInvariant();
  return name is {Length:>=3 and <=32}&&char.IsAsciiLetterOrDigit(name[0])&&
   name.All(c=>char.IsAsciiLetterOrDigit(c)||c is '.' or '_' or '-')?name:null;
 }

 private static async Task<AppUser> FindClient(OrbeDb db,string username)
 {
  if(NormalizeUsername(username) is {} name)
  {
   var user=await (from u in db.Users join ur in db.UserRoles on u.Id equals ur.UserId
    join r in db.Roles on ur.RoleId equals r.Id
    where u.BookingUsername==name&&u.Active&&u.EmailConfirmed&&r.Name==Roles.Client select u).SingleOrDefaultAsync();
   if(user is not null)return user;
  }
  throw new ApiError(404,"Usuário de cliente não encontrado ou conta indisponível.");
 }

 private static async Task<IResult> Create(RecurringInput input,Actor actor,OrbeDb db,TimeProvider clock,Scheduling scheduling)
 {
  var employee=await actor.Employee(input.EmployeeId);
  var interval=input.EveryDays??(input.EveryWeeks is >=1 and <=12?input.EveryWeeks.Value*7:0);
  if(input.SeriesId==Guid.Empty||string.IsNullOrWhiteSpace(input.Title)||input.Title.Length>200||
     input.Notes?.Length>2000||input.Kind is not ("Atendimento" or "Compromisso" or "Folga" or "Outro")||
     input.StartTime>=input.EndTime||input.Count is <2 or >52||interval is <1 or >365||input.ExpectedCents<0||
     input.Kind!="Atendimento"&&(!string.IsNullOrWhiteSpace(input.ClientUsername)||input.ExpectedCents!=0))
   throw new ApiError(400,"Confira os dados, os horários e a frequência da série.");
  var policy=(await scheduling.Settings()).Policy();
  var zone=TimeZoneInfo.FindSystemTimeZoneById(policy.TimeZoneId);
  var now=clock.GetUtcNow();
  var today=DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now,zone).DateTime);
  var span=(input.Count-1)*interval;
  if(input.FirstDate<today||input.FirstDate.DayNumber+span>today.AddYears(1).DayNumber)
   throw new ApiError(400,"Escolha datas futuras, dentro dos próximos 12 meses.");
  await using var tx=await db.Database.BeginTransactionAsync();
  string? client=null;
  if(!string.IsNullOrWhiteSpace(input.ClientUsername))
  {
   client=(await FindClient(db,input.ClientUsername)).Id;
   await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"AspNetUsers\" WHERE \"Id\"={client} FOR UPDATE");
  }
  await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Employees\" WHERE \"Id\"={employee} FOR UPDATE");
  var series=input.SeriesId.ToString("N");
  var prior=await db.Appointments.Where(x=>x.SeriesId==input.SeriesId).ToArrayAsync();
  if(prior.Length>0)
  {
   if(prior.Length!=input.Count||prior.Any(x=>x.EmployeeId!=employee||x.ClientId!=client||x.Title!=input.Title.Trim()||
      x.Notes!=input.Notes||x.ExpectedCents!=input.ExpectedCents||x.Kind!=input.Kind)||
      prior.OrderBy(x=>x.Start).Where((x,i)=>x.Start!=Instant(input.FirstDate.AddDays(i*interval),input.StartTime,zone)||
       x.End!=Instant(input.FirstDate.AddDays(i*interval),input.EndTime,zone)).Any())
    throw new ApiError(409,"Esta confirmação já foi usada para outra série ou as ocorrências foram alteradas.");
   return Results.Ok(new{count=prior.Length,pending=prior.Count(x=>x.Status=="Pending")});
  }
  var hours=await scheduling.Hours(employee);
  var rows=new List<ScheduleItem>();
  for(var i=0;i<input.Count;i++)
  {
   var date=input.FirstDate.AddDays(i*interval);
   var start=Instant(date,input.StartTime,zone);
   var end=Instant(date,input.EndTime,zone);
   if(start<=now||end<=start)throw new ApiError(400,"O primeiro horário precisa estar no futuro e o término depois do início.");
   var conflict=await db.Appointments.AnyAsync(x=>x.Status=="Confirmed"&&(x.EmployeeId==employee||client!=null&&x.ClientId==client)&&x.Start<end&&x.End>start);
   var pending=input.Kind=="Atendimento"&&input.NotifyUnavailable&&
    (conflict||!Scheduling.Fits(start,end,policy,hours.Week,hours.Days));
   if(conflict&&!pending)
    throw new ApiError(409,$"Já existe um agendamento em {date:dd/MM/yyyy}. Nenhuma ocorrência foi criada.");
   rows.Add(new(){Id=$"{series}-{i}",SeriesId=input.SeriesId,EmployeeId=employee,ClientId=client,
    Title=input.Title.Trim(),Kind=input.Kind,Status=pending?"Pending":"Confirmed",Start=start,End=end,RecordedAt=now,Notes=input.Notes,
    ExpectedCents=input.ExpectedCents,DurationMinutes=(int)(end-start).TotalMinutes});
  }
  db.Appointments.AddRange(rows);
  await db.SaveChangesAsync();
  await tx.CommitAsync();
  return Results.Ok(new{count=rows.Count,pending=rows.Count(x=>x.Status=="Pending")});
 }

 private static DateTimeOffset Instant(DateOnly date,TimeOnly time,TimeZoneInfo zone)
 {
  var local=date.ToDateTime(time);
  if(zone.IsInvalidTime(local)||zone.IsAmbiguousTime(local))throw new ApiError(400,"Um horário é inválido ou ambíguo no fuso do estabelecimento.");
  return TimeZoneInfo.ConvertTimeToUtc(local,zone);
 }
}
