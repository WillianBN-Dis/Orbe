using Microsoft.EntityFrameworkCore;

namespace Orbe.Server;

public sealed class ClientRelationship : IBusinessRecord
{
 public int BusinessId {get;set;}
 public string EmployeeId {get;set;}="";
 public string ClientId {get;set;}="";
}

public static class Birthdays
{
 public static void MapBirthdays(this RouteGroupBuilder team)=>team.MapGet("/birthdays",async(int year,string? employeeId,Actor actor,OrbeDb db)=>{
  if(year is <1900 or >2100)throw new ApiError(400,"Ano inválido.");
  var employee=actor.Owner?employeeId:await actor.Employee(employeeId,true);
  // Keep the relationship even when its last booking is deleted. Read the account date so
  // corrections propagate without copying personal information into events.
  var clients=await db.Users.Where(u=>u.Active&&u.BirthDate!=null&&db.ClientRelationships.Any(a=>a.ClientId==u.Id&&(employee==null||a.EmployeeId==employee)))
   .Select(u=>new{u.Id,u.FullName,u.BirthDate}).ToArrayAsync();
  return Results.Ok(clients.Select(u=>new{clientId=u.Id,name=u.FullName,month=u.BirthDate!.Value.Month,day=u.BirthDate.Value.Day,date=new DateOnly(year,u.BirthDate!.Value.Month,
   Math.Min(u.BirthDate.Value.Day,DateTime.DaysInMonth(year,u.BirthDate.Value.Month)))}).OrderBy(u=>u.date).ThenBy(u=>u.name));
 });
}
