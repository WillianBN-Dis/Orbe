using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Orbe.Server;

public sealed record BusinessInput(string Name,string Slug,string OwnerUsername);
public sealed record MembershipInput(string Username,bool Administrator=false);

public static class Businesses
{
 public static async Task Select(HttpContext context,RequestDelegate next)
 {
  if(!context.Request.Path.StartsWithSegments("/api/v1")){await next(context);return;}
  var db=context.RequestServices.GetRequiredService<OrbeDb>();
  var slug=context.Request.Headers["X-Orbe-Business"].ToString();
  if(slug.Length>60)throw new ApiError(400,"Endereço da empresa inválido.");
  if(slug.Length>0&&!context.Request.Path.Equals("/api/v1/public/connection",StringComparison.OrdinalIgnoreCase))
  {
   db.BusinessId=await db.Settings.IgnoreQueryFilters().Where(x=>x.Slug==slug).Select(x=>x.Id).SingleOrDefaultAsync();
   if(db.BusinessId==0)throw new ApiError(404,"Empresa não encontrada.");
  }
  if(context.User.Identity is ClaimsIdentity identity&&context.User.FindFirstValue(ClaimTypes.NameIdentifier) is {} user)
  {
   if(context.User.IsInRole(Roles.Owner))identity.AddClaim(new("orbe:platform-owner","true"));
   if(db.BusinessId!=1||!context.User.IsInRole(Roles.Owner))
   {
    foreach(var role in identity.FindAll(ClaimTypes.Role).Where(x=>x.Value is Roles.Owner or Roles.Staff).ToArray())identity.RemoveClaim(role);
    var member=await db.Employees.SingleOrDefaultAsync(x=>x.UserId==user&&x.Enabled);
    if(member is not null)identity.AddClaim(new(ClaimTypes.Role,db.BusinessId!=1&&member.Administrator?Roles.Owner:Roles.Staff));
   }
  }
  await next(context);
 }

 public static void MapBusinesses(this WebApplication app)
 {
  var api=app.MapGroup("/api/v1");
  api.MapGet("/public/businesses",async(string? q,OrbeDb db)=>{
   var query=db.Settings.IgnoreQueryFilters();
   if(!string.IsNullOrWhiteSpace(q))
   {
    var term=q.Trim();
    if(term.Length>100)throw new ApiError(400,"Nome muito longo.");
    var pattern="%"+term.Replace("\\","\\\\").Replace("%","\\%").Replace("_","\\_")+"%";
    query=query.Where(x=>EF.Functions.ILike(x.Name,pattern)||EF.Functions.ILike(x.Slug,pattern));
   }
   return await query.OrderBy(x=>x.Name).Take(30).Select(x=>new{x.Slug,x.Name}).ToArrayAsync();
  });
  api.MapGet("/businesses/mine",async(Actor actor,OrbeDb db)=>{
   var platform=actor.User.HasClaim("orbe:platform-owner","true");
   return await (from e in db.Employees.IgnoreQueryFilters() join b in db.Settings.IgnoreQueryFilters() on e.BusinessId equals b.Id
    where e.UserId==actor.Id&&e.Enabled select new{b.Name,b.Slug}).Union(
    db.Settings.IgnoreQueryFilters().Where(b=>b.Id==1&&platform).Select(b=>new{b.Name,b.Slug}))
    .OrderBy(x=>x.Name).ToArrayAsync();
  }).RequireAuthorization();
  api.MapPost("/platform/businesses",async(BusinessInput input,Actor actor,OrbeDb db,UserManager<AppUser> users)=>{
   if(!actor.User.HasClaim("orbe:platform-owner","true"))throw new ApiError(403,"Acesso exclusivo da administração do Orbe.");
   if(db.BusinessId!=1)throw new ApiError(400,"Abra o espaço principal para cadastrar empresas.");
   var slug=input.Slug?.Trim().ToLowerInvariant();
   if(slug is null||!Regex.IsMatch(slug,"^[a-z0-9](?:[a-z0-9-]{1,58}[a-z0-9])$")||
      slug is "orbe" or "api" or "health" or "entrar" or "equipe" or "confirmar-email" or "redefinir-senha" or "recuperar-senha"||
      string.IsNullOrWhiteSpace(input.Name)||input.Name.Trim().Length>150)
    throw new ApiError(400,"Confira o nome e o endereço da empresa.");
   var owner=await users.FindByNameAsync(input.OwnerUsername)??await users.FindByEmailAsync(input.OwnerUsername);
   if(owner is null||!owner.Active||!owner.EmailConfirmed)throw new ApiError(400,"Selecione uma conta ativa e confirmada.");
   await using var tx=await db.Database.BeginTransactionAsync();
   await db.Database.ExecuteSqlRawAsync("SELECT 1 FROM \"Settings\" WHERE \"Id\"=1 FOR UPDATE");
   if(await db.Settings.IgnoreQueryFilters().AnyAsync(x=>x.Slug==slug))throw new ApiError(409,"Este endereço de empresa já está em uso.");
   var b=new BusinessSettings{Id=0,Name=input.Name.Trim(),Slug=slug,ImportCompleted=true};
   db.Settings.Add(b);await db.SaveChangesAsync();
   db.BusinessId=b.Id;
   var administrator=new Employee{UserId=owner.Id,DisplayName=owner.FullName,Administrator=true};
   db.Employees.Add(administrator);
   await MembershipCatalog.Copy(db,administrator,b.Id);
   await db.SaveChangesAsync();await tx.CommitAsync();
   return Results.Ok(new{b.Name,b.Slug});
  }).RequireAuthorization();
  api.MapPost("/owner/memberships",async(MembershipInput input,OrbeDb db,UserManager<AppUser> users)=>{
   var user=await users.FindByNameAsync(input.Username)??await users.FindByEmailAsync(input.Username);
   if(user is null||!user.Active||!user.EmailConfirmed)throw new ApiError(400,"Selecione uma conta ativa e confirmada.");
   await using var tx=await db.Database.BeginTransactionAsync();
   await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Settings\" WHERE \"Id\"={db.BusinessId} FOR UPDATE");
   if(await db.Employees.AnyAsync(x=>x.UserId==user.Id))throw new ApiError(409,"A conta já faz parte da empresa.");
   var employee=new Employee{UserId=user.Id,DisplayName=user.FullName,Administrator=input.Administrator&&db.BusinessId!=1};
   db.Employees.Add(employee);await MembershipCatalog.Copy(db,employee,db.BusinessId);await db.SaveChangesAsync();await tx.CommitAsync();
   return Results.Ok(new{employee.Id,employee.DisplayName});
  }).RequireAuthorization(p=>p.RequireRole(Roles.Owner));
 }
}
