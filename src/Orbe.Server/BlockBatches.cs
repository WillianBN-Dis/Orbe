using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Orbe.Server;

public sealed record BlockBatchInput(Guid BatchId,string? EmployeeId,string Title,string Kind,DateOnly[] Dates,
 bool AllDay,TimeOnly StartTime,TimeOnly EndTime,string? Notes,bool EndsAtMidnight=false);

public static class BlockBatches
{
 public static void MapBlockBatches(this RouteGroupBuilder team)=>team.MapPost("/blocks/batch",Create);

 private static async Task<IResult> Create(BlockBatchInput input,Actor actor,OrbeDb db)
 {
  var employee=await actor.Employee(input.EmployeeId);
  if(input.BatchId==Guid.Empty||string.IsNullOrWhiteSpace(input.Title)||input.Title.Length>200||
   input.Kind is not ("Folga" or "Compromisso" or "Outro")||input.Notes?.Length>2000||
   input.Dates is not {Length:>=1 and <=366}||input.Dates.Distinct().Count()!=input.Dates.Length||
   input.Dates.Any(d=>d.Year is <1900 or >2100)||
   !input.AllDay&&!input.EndsAtMidnight&&input.EndTime<=input.StartTime)
   throw new ApiError(400,"Confira as datas, o título e os horários. Selecione até 366 dias distintos.");
  var settings=await db.Settings.SingleAsync();
  var zone=TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZoneId);
  DateTimeOffset Instant(DateOnly date,TimeOnly time){
   var local=date.ToDateTime(time);
   if(zone.IsInvalidTime(local)||zone.IsAmbiguousTime(local))throw new ApiError(400,"Há um horário inválido ou ambíguo nas datas selecionadas.");
   return TimeZoneInfo.ConvertTimeToUtc(local,zone);
  }
  var prefix=input.BatchId.ToString("N")+"-";
  var rows=input.Dates.Order().Select(date=>new ScheduleItem{
   Id=prefix+date.ToString("yyyyMMdd"),EmployeeId=employee,Title=input.Title.Trim(),Kind=input.Kind,Notes=input.Notes,
   Start=Instant(date,input.AllDay?TimeOnly.MinValue:input.StartTime),
   End=Instant(input.AllDay||input.EndsAtMidnight?date.AddDays(1):date,input.AllDay||input.EndsAtMidnight?TimeOnly.MinValue:input.EndTime)
  }).ToArray();
  await using var tx=await db.Database.BeginTransactionAsync();
  await db.Database.ExecuteSqlInterpolatedAsync($"""SELECT 1 FROM "AspNetUsers" WHERE "Id"={actor.Id} FOR UPDATE""");
  await db.Database.ExecuteSqlInterpolatedAsync($"""SELECT 1 FROM "Employees" WHERE "Id"={employee} FOR UPDATE""");
  var key="blocks:"+input.BatchId.ToString("N");
  var hash=Secrets.Hash(JsonSerializer.Serialize(rows.Select(r=>new{r.Id,r.EmployeeId,r.Title,r.Kind,r.Start,r.End,r.Notes})));
  var request=await db.BookingRequests.SingleOrDefaultAsync(r=>r.UserId==actor.Id&&r.Key==key);
  if(request is not null&&request.RequestHash!=hash)throw new ApiError(409,"Esta confirmação já foi usada para outros dados.");
  var previous=await db.Appointments.Where(x=>x.Id.StartsWith(prefix)).ToArrayAsync();
  if(previous.Length>0||request is not null){
   if(previous.Length!=rows.Length||previous.Any(old=>!rows.Any(r=>r.Id==old.Id&&r.EmployeeId==old.EmployeeId&&
    r.Title==old.Title&&r.Kind==old.Kind&&r.Notes==old.Notes&&r.Start==old.Start&&r.End==old.End&&old.Status=="Confirmed")))
    throw new ApiError(409,"Esta confirmação já foi usada ou os registros foram alterados. Atualize a lista.");
   return Results.Ok(new{count=previous.Length});
  }
  var from=rows.Min(r=>r.Start);var until=rows.Max(r=>r.End);
  var occupied=await db.Appointments.Where(a=>a.EmployeeId==employee&&a.Status=="Confirmed"&&a.Start<until&&a.End>from)
   .Select(a=>new{a.Id,a.Title,a.Start,a.End}).ToArrayAsync();
  var conflicts=rows.Where(r=>occupied.Any(a=>a.Start<r.End&&a.End>r.Start))
   .Select(r=>DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(r.Start,zone).DateTime)).ToArray();
  if(conflicts.Length>0)throw new ApiError(409,"Há eventos confirmados em "+string.Join(", ",conflicts.Take(8).Select(d=>d.ToString("dd/MM/yyyy")))+". Revise essas datas antes de salvar.",conflicts);
  db.Appointments.AddRange(rows);
  db.BookingRequests.Add(new(){UserId=actor.Id,Key=key,RequestHash=hash,AppointmentId=rows[0].Id});
  await db.SaveChangesAsync();await tx.CommitAsync();
  return Results.Ok(new{count=rows.Length});
 }
}
