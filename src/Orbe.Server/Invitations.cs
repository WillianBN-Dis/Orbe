using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Orbe.Server;

public static class Invitations
{
 public sealed record InviteInput(string Code);
 public sealed record Decision(bool Approve);
 internal static string NewCode()=>Convert.ToHexString(RandomNumberGenerator.GetBytes(12));
 internal static async Task<BusinessSettings> Find(OrbeDb db,string? code)
 {
  var value=code?.Trim().ToUpperInvariant();
  if(value is null||value.Length!=24||!value.All(Uri.IsHexDigit))throw new ApiError(400,"Código de convite inválido.");
  return await db.Settings.IgnoreQueryFilters().SingleOrDefaultAsync(b=>b.InvitationCode==value)
   ??throw new ApiError(400,"Código de convite inválido.");
 }
 internal static async Task Request(OrbeDb db,AppUser user,BusinessSettings business,TimeProvider clock)
 {
  await db.Database.ExecuteSqlInterpolatedAsync($"""SELECT 1 FROM "Settings" WHERE "Id"={business.Id} FOR UPDATE""");
  await db.Entry(business).ReloadAsync();
  if(await db.Employees.IgnoreQueryFilters().AnyAsync(e=>e.BusinessId==business.Id&&e.UserId==user.Id))
   throw new ApiError(409,"A conta já possui um vínculo com a empresa. Consulte o administrador.");
  var prior=await db.MembershipApplications.IgnoreQueryFilters().SingleOrDefaultAsync(x=>x.BusinessId==business.Id&&x.UserId==user.Id);
  if(prior is null){db.MembershipApplications.Add(new(){UserId=user.Id,RequestedAt=clock.GetUtcNow()});}
  else{prior.Status="Pending";prior.RequestedAt=clock.GetUtcNow();}
 }
 public static void MapInvitations(this WebApplication app)
 {
  var api=app.MapGroup("/api/v1").RequireAuthorization();
  api.MapGet("/memberships/requests/mine",async(Actor actor,OrbeDb db)=>
   await (from r in db.MembershipApplications.IgnoreQueryFilters() join b in db.Settings.IgnoreQueryFilters() on r.BusinessId equals b.Id
    where r.UserId==actor.Id select new{r.Status,b.Name,b.Slug}).ToArrayAsync());
  api.MapPost("/memberships/request",async(InviteInput input,Actor actor,OrbeDb db,TimeProvider clock)=>{
   var business=await Find(db,input.Code);
   var user=await db.Users.SingleAsync(u=>u.Id==actor.Id);
   if(!user.EmailConfirmed||!user.Active)throw new ApiError(403,"Confirme seu e-mail antes de solicitar ingresso.");
   await using var tx=await db.Database.BeginTransactionAsync();
   db.BusinessId=business.Id;await Request(db,user,business,clock);
   if(business.InvitationCode!=input.Code.Trim().ToUpperInvariant())throw new ApiError(409,"O convite foi alterado. Solicite o código atual.");
   await db.SaveChangesAsync();await tx.CommitAsync();return Results.Ok(new{message="Solicitação enviada. Aguarde a aprovação do administrador."});
  }).RequireRateLimiting("auth");
  api.MapGet("/owner/invitation",async(OrbeDb db)=>{
   var business=await db.Settings.SingleAsync();
   return Results.Ok(new{code=business.InvitationCode});
  }).RequireAuthorization(p=>p.RequireRole(Roles.Owner));
  api.MapGet("/owner/membership-requests",async(OrbeDb db)=>await (
   from r in db.MembershipApplications join u in db.Users on r.UserId equals u.Id
   where r.Status=="Pending" select new{r.Id,u.FullName,u.Email,u.EmailConfirmed,r.RequestedAt}).ToArrayAsync()
  ).RequireAuthorization(p=>p.RequireRole(Roles.Owner));
  api.MapPut("/owner/membership-requests/{id}",async(string id,Decision input,OrbeDb db)=>{
   await using var tx=await db.Database.BeginTransactionAsync();
   await db.Database.ExecuteSqlInterpolatedAsync($"""SELECT 1 FROM "Settings" WHERE "Id"={db.BusinessId} FOR UPDATE""");
   var row=await db.MembershipApplications.SingleOrDefaultAsync(r=>r.Id==id)??throw new ApiError(404,"Solicitação não encontrada.");
   if(row.Status!="Pending")throw new ApiError(409,"Solicitação já analisada.");
   if(input.Approve){
    var user=await db.Users.SingleAsync(u=>u.Id==row.UserId);
    if(!user.Active||!user.EmailConfirmed)throw new ApiError(409,"A conta precisa estar ativa e com e-mail confirmado.");
    if(await db.Employees.AnyAsync(e=>e.UserId==row.UserId))throw new ApiError(409,"A conta já faz parte da equipe.");
    var employee=new Employee{UserId=row.UserId,DisplayName=user.FullName};
    db.Employees.Add(employee);await MembershipCatalog.Copy(db,employee);
   }
   row.Status=input.Approve?"Approved":"Rejected";
   await db.SaveChangesAsync();await tx.CommitAsync();return Results.NoContent();
  }).RequireAuthorization(p=>p.RequireRole(Roles.Owner));
  api.MapGet("/platform/invitations",async(Actor actor,OrbeDb db)=>{
   Platform(actor);
   return await db.Settings.IgnoreQueryFilters().OrderBy(b=>b.Name).Select(b=>new{b.Id,b.Name,b.Slug,code=b.InvitationCode}).ToArrayAsync();
  });
  api.MapPost("/platform/invitations/{id:int}/rotate",async(int id,Actor actor,OrbeDb db)=>{
   Platform(actor);await using var tx=await db.Database.BeginTransactionAsync();
   await db.Database.ExecuteSqlInterpolatedAsync($"""SELECT 1 FROM "Settings" WHERE "Id"={id} FOR UPDATE""");
   var business=await db.Settings.IgnoreQueryFilters().SingleOrDefaultAsync(b=>b.Id==id)??throw new ApiError(404,"Empresa não encontrada.");
   business.InvitationCode=NewCode();await db.SaveChangesAsync();await tx.CommitAsync();return Results.Ok(new{code=business.InvitationCode});
  });
 }
 private static void Platform(Actor actor)
 {if(!actor.User.HasClaim("orbe:platform-owner","true"))throw new ApiError(403,"Somente a administração do Orbe pode alterar convites.");}
}
