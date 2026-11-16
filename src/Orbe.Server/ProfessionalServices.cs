using Microsoft.EntityFrameworkCore;

namespace Orbe.Server;

public static class ProfessionalServices
{
 public static void MapProfessionalServices(this RouteGroupBuilder team)
 {
  team.MapGet("/categories",async(string? employeeId,Actor actor,OrbeDb db)=>{
   var employee=await CategoryEmployee(employeeId,actor,db);
   return await db.Categories.Where(x=>x.EmployeeId==employee).OrderBy(x=>x.Name).Select(x=>new{x.Id,x.Name}).ToArrayAsync();
  });
  team.MapPost("/categories",async(string? employeeId,CategoryInput input,Actor actor,OrbeDb db)=>{
   var name=input.Name?.Trim();
   if(string.IsNullOrWhiteSpace(name)||name.Length>80)throw new ApiError(400,"Informe um nome de categoria de até 80 caracteres.");
   var key=name.ToUpperInvariant();
   var employee=await CategoryEmployee(employeeId,actor,db);
   await using var tx=await db.Database.BeginTransactionAsync();await Lock(db,employee);
   var row=await db.Categories.SingleOrDefaultAsync(x=>x.EmployeeId==employee&&x.NormalizedName==key);
   if(row is not null)return Results.Ok(new{row.Id,row.Name});
   row=new(){EmployeeId=employee,Name=name,NormalizedName=key};db.Categories.Add(row);await db.SaveChangesAsync();await tx.CommitAsync();
   return Results.Ok(new{row.Id,row.Name});
  });
  team.MapGet("/services",async(string? employeeId,Actor actor,OrbeDb db)=>{
   var employee=actor.Owner?employeeId:await actor.Employee(employeeId);
   return Results.Ok(await (from link in db.EmployeeServices join item in db.Services on link.ServiceId equals item.Id join person in db.Employees on link.EmployeeId equals person.Id where employee==null||link.EmployeeId==employee orderby person.DisplayName,link.Name??item.Name select new{item.Id,link.EmployeeId,link.CategoryId,EmployeeName=person.DisplayName,Name=link.Name??item.Name,DurationMinutes=link.DurationMinutes??item.DurationMinutes,PriceCents=link.PriceCents??item.PriceCents,PriceFrom=link.PriceFrom??item.PriceFrom,Enabled=link.Enabled??item.Enabled,link.Version}).ToArrayAsync());
  });
  team.MapPost("/services",async(string? employeeId,ServiceInput input,Actor actor,OrbeDb db)=>{
   var employee=await actor.Employee(employeeId,actor.Owner);Validate(input);await ValidateCategory(db,employee,input.CategoryId);
   await using var tx=await db.Database.BeginTransactionAsync();await Lock(db,employee);
   var item=new Service{Name=input.Name.Trim(),DurationMinutes=input.DurationMinutes,PriceCents=input.PriceCents,PriceFrom=input.PriceFrom??false,Enabled=input.Enabled};db.Services.Add(item);db.EmployeeServices.Add(new(){EmployeeId=employee,ServiceId=item.Id,CategoryId=input.CategoryId});
   var person=await db.Employees.SingleAsync(x=>x.Id==employee);person.Version=Guid.NewGuid();await db.SaveChangesAsync();await tx.CommitAsync();return Results.Ok(new{item.Id,EmployeeId=employee});
  });
  team.MapPut("/services/{id}",async(string id,string? employeeId,ServiceInput input,Actor actor,OrbeDb db)=>{
   var employee=await actor.Employee(employeeId,true);Validate(input);await ValidateCategory(db,employee,input.CategoryId);
   await using var tx=await db.Database.BeginTransactionAsync();await Lock(db,employee);
   var link=await db.EmployeeServices.SingleOrDefaultAsync(x=>x.EmployeeId==employee&&x.ServiceId==id)??throw new ApiError(404,"Serviço deste profissional não encontrado.");
   if(link.Version!=input.Version)throw new ApiError(409,"Serviço alterado. Atualize a lista.");
   link.CategoryId=input.CategoryId;link.Name=input.Name.Trim();link.DurationMinutes=input.DurationMinutes;link.PriceCents=input.PriceCents;link.PriceFrom=input.PriceFrom??link.PriceFrom;link.Enabled=input.Enabled;link.Version=Guid.NewGuid();
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
 private sealed record CategoryInput(string Name);
 private static async Task<string> CategoryEmployee(string? requested,Actor actor,OrbeDb db)
 {
  if(requested is null&&actor.Owner)
   requested=await db.Employees.Where(x=>x.UserId==actor.Id).Select(x=>x.Id).SingleOrDefaultAsync();
  return await actor.Employee(requested,true);
 }
 private static async Task ValidateCategory(OrbeDb db,string employee,string? category)
 {if(category is not null&&!await db.Categories.AnyAsync(x=>x.EmployeeId==employee&&x.Id==category))throw new ApiError(400,"Categoria inválida para este profissional.");}
 private static Task<int> Lock(OrbeDb db,string employee)=>db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Employees\" WHERE \"Id\"={employee} FOR UPDATE");
 private static void Validate(ServiceInput input)
 {if(string.IsNullOrWhiteSpace(input.Name)||input.Name.Length>150||input.DurationMinutes is <1 or >1440||input.PriceCents<0)throw new ApiError(400,"Confira nome, duração e preço do serviço.");}
}
