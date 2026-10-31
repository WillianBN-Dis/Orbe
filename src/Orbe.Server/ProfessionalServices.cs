using Microsoft.EntityFrameworkCore;

namespace Orbe.Server;

public static class ProfessionalServices
{
 public static void MapProfessionalServices(this RouteGroupBuilder team)
 {
  team.MapGet("/services",async(string? employeeId,Actor actor,OrbeDb db)=>{
   var employee=actor.Owner?employeeId:await actor.Employee(employeeId);
   return Results.Ok(await (from link in db.EmployeeServices join item in db.Services on link.ServiceId equals item.Id join person in db.Employees on link.EmployeeId equals person.Id where employee==null||link.EmployeeId==employee orderby person.DisplayName,link.Name??item.Name select new{item.Id,link.EmployeeId,EmployeeName=person.DisplayName,Name=link.Name??item.Name,DurationMinutes=link.DurationMinutes??item.DurationMinutes,PriceCents=link.PriceCents??item.PriceCents,Enabled=link.Enabled??item.Enabled,link.Version}).ToArrayAsync());
  });
  team.MapPost("/services",async(string? employeeId,ServiceInput input,Actor actor,OrbeDb db)=>{
   var employee=await actor.Employee(employeeId,actor.Owner);Validate(input);
   await using var tx=await db.Database.BeginTransactionAsync();await Lock(db,employee);
   var item=new Service{Name=input.Name.Trim(),DurationMinutes=input.DurationMinutes,PriceCents=input.PriceCents,Enabled=input.Enabled};db.Services.Add(item);db.EmployeeServices.Add(new(){EmployeeId=employee,ServiceId=item.Id});
   var person=await db.Employees.SingleAsync(x=>x.Id==employee);person.Version=Guid.NewGuid();await db.SaveChangesAsync();await tx.CommitAsync();return Results.Ok(new{item.Id,EmployeeId=employee});
  });
  team.MapPut("/services/{id}",async(string id,string? employeeId,ServiceInput input,Actor actor,OrbeDb db)=>{
   var employee=await actor.Employee(employeeId,true);Validate(input);
   await using var tx=await db.Database.BeginTransactionAsync();await Lock(db,employee);
   var link=await db.EmployeeServices.SingleOrDefaultAsync(x=>x.EmployeeId==employee&&x.ServiceId==id)??throw new ApiError(404,"Serviço deste profissional não encontrado.");
   if(link.Version!=input.Version)throw new ApiError(409,"Serviço alterado. Atualize a lista.");
   link.Name=input.Name.Trim();link.DurationMinutes=input.DurationMinutes;link.PriceCents=input.PriceCents;link.Enabled=input.Enabled;link.Version=Guid.NewGuid();
   (await db.Employees.SingleAsync(x=>x.Id==employee)).Version=Guid.NewGuid();await db.SaveChangesAsync();await tx.CommitAsync();return Results.Ok(link);
  });
  team.MapDelete("/services/{id}",async(string id,string? employeeId,[Microsoft.AspNetCore.Mvc.FromBody] VersionInput input,Actor actor,OrbeDb db)=>{
   var employee=await actor.Employee(employeeId,true);
   await using var tx=await db.Database.BeginTransactionAsync();await Lock(db,employee);
   var link=await db.EmployeeServices.SingleOrDefaultAsync(x=>x.EmployeeId==employee&&x.ServiceId==id)??throw new ApiError(404,"Serviço deste profissional não encontrado.");
   if(link.Version!=input.Version)throw new ApiError(409,"Serviço alterado. Atualize a lista.");
   db.EmployeeServices.Remove(link);(await db.Employees.SingleAsync(x=>x.Id==employee)).Version=Guid.NewGuid();await db.SaveChangesAsync();await tx.CommitAsync();return Results.NoContent();
  });
 }
 private static Task<int> Lock(OrbeDb db,string employee)=>db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Employees\" WHERE \"Id\"={employee} FOR UPDATE");
 private static void Validate(ServiceInput input)
 {if(string.IsNullOrWhiteSpace(input.Name)||input.Name.Length>150||input.DurationMinutes is <1 or >1440||input.PriceCents<0)throw new ApiError(400,"Confira nome, duração e preço do serviço.");}
}
