using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Orbe.Server;
public sealed record EmployeeInput(string UserName,string Email,string FullName,string Password);
public sealed record EmployeeChange(Guid Version,string DisplayName,bool Enabled,string[] Services);
public sealed record ServiceInput(string Name,int DurationMinutes,long PriceCents,bool Enabled,Guid? Version);
public sealed record PolicyInput(Guid Version,string Name,bool ImportCompleted,bool PublicBookingOpen,int SlotMinutes,int LeadMinutes,int HorizonDays,int CancellationHours,string TimeZoneId);
public sealed record ResetStaffInput(string Password);

public static class Routes
{
 private static void Text(string? value,int max,string label){if(string.IsNullOrWhiteSpace(value)||value.Length>max)throw new ApiError(400,$"Confira {label}.");}
 public static void MapOrbe(this WebApplication app)
 {
  var api=app.MapGroup("/api/v1");
  api.MapGet("/public/info",async(OrbeDb db,IEmailDelivery email)=>{var s=await db.Settings.SingleAsync();return Results.Ok(new{s.Name,s.PublicBookingOpen,s.TimeZoneId,s.CancellationHours,emailReady=email.Ready});});
  api.MapGet("/public/services",async(OrbeDb db)=>Results.Ok(await db.Services.Where(x=>x.Enabled).OrderBy(x=>x.Name).Select(x=>new{x.Id,x.Name,x.DurationMinutes,x.PriceCents}).ToArrayAsync()));
  api.MapGet("/public/employees",async(string? serviceId,OrbeDb db)=>Results.Ok(await db.Employees.Where(x=>x.Enabled&&x.User.Active&&(serviceId==null||db.EmployeeServices.Any(e=>e.EmployeeId==x.Id&&e.ServiceId==serviceId))).OrderBy(x=>x.DisplayName).Select(x=>new{x.Id,x.DisplayName}).ToArrayAsync()));
  api.MapGet("/public/availability",async(string employeeId,string serviceId,DateOnly from,int? days,Scheduling scheduling)=>Results.Ok(await scheduling.AvailabilityFor(employeeId,serviceId,from,days??1)));
  var team=api.MapGroup("/team").RequireAuthorization(p=>p.RequireRole(Roles.Owner,Roles.Staff));
  team.MapGet("/employees",async(Actor actor,OrbeDb db)=>Results.Ok(await db.Employees.Where(x=>actor.Owner||x.UserId==actor.Id).OrderBy(x=>x.DisplayName).Select(x=>new{x.Id,x.DisplayName,x.Enabled,x.Version}).ToArrayAsync()));
  team.MapGet("/hours",async(string? employeeId,Scheduling s)=>Results.Ok(await s.ReadHours(employeeId)));
  team.MapPut("/hours",async(string? employeeId,HoursInput input,Scheduling s)=>{await s.SaveHours(employeeId,input);return Results.Ok(await s.ReadHours(employeeId));});
  team.MapPost("/blocks",async(BlockInput input,Scheduling s)=>Results.Ok(await s.Block(input)));
  team.MapGet("/appointments",async(string? employeeId,DateTimeOffset? from,DateTimeOffset? until,int? offset,Actor actor,OrbeDb db)=>
  {
   var id=actor.Owner?employeeId:await actor.Employee(employeeId);var skip=Math.Max(0,offset??0);
   var query=db.Appointments.AsNoTracking().Where(x=>id==null||x.EmployeeId==id);
   if(from is not null)query=query.Where(x=>x.End>from);if(until is not null)query=query.Where(x=>x.Start<until);
   return Results.Ok(await query.OrderBy(x=>x.Start).ThenBy(x=>x.Id).Skip(skip).Take(200).Select(x=>new{x.Id,x.EmployeeId,x.ClientId,x.ServiceId,x.Title,x.Kind,x.Status,x.Start,x.End,x.LegacyClient,x.Notes,x.ExpectedCents,x.DurationMinutes,x.Version,clientName=x.ClientId==null?x.LegacyClient:db.Users.Where(u=>u.Id==x.ClientId).Select(u=>u.FullName).FirstOrDefault(),clientPhone=db.Users.Where(u=>u.Id==x.ClientId).Select(u=>u.PhoneNumber).FirstOrDefault()}).ToArrayAsync());
  });
  team.MapPost("/appointments",async(TeamAppointmentInput input,Actor actor,OrbeDb db,TimeProvider clock)=>
  {
   var id=await actor.Employee(input.EmployeeId);Text(input.Title,200,"o título");
   if(input.End<=input.Start||input.End-input.Start>TimeSpan.FromDays(1)||input.ExpectedCents<0||input.Notes?.Length>2000||input.LegacyClient?.Length>150)throw new ApiError(400,"Confira o período e os valores do agendamento.");
   if(input.ClientId is not null&&!await db.UserRoles.Join(db.Roles,x=>x.RoleId,x=>x.Id,(u,r)=>new{u.UserId,r.Name}).AnyAsync(x=>x.UserId==input.ClientId&&x.Name==Roles.Client))throw new ApiError(400,"Cliente inválido.");
   await using var tx=await db.Database.BeginTransactionAsync();await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Employees\" WHERE \"Id\"={id} FOR UPDATE");
   var item=new ScheduleItem{EmployeeId=id,Title=input.Title.Trim(),Start=input.Start.ToUniversalTime(),End=input.End.ToUniversalTime(),ClientId=input.ClientId,LegacyClient=input.LegacyClient,Notes=input.Notes,ExpectedCents=input.ExpectedCents,DurationMinutes=(int)(input.End-input.Start).TotalMinutes};
   db.Appointments.Add(item);await db.SaveChangesAsync();await tx.CommitAsync();return Results.Ok(item);
  });
  team.MapPut("/appointments/{id}",async(string id,TeamAppointmentInput input,Actor actor,OrbeDb db,Scheduling s)=>
  {
   var item=await s.Accessible(id,true);Text(input.Title,200,"o título");
   if(item.Kind!="Atendimento"||item.Status!="Confirmed"||item.EmployeeId!=await actor.Employee(input.EmployeeId)||input.End<=input.Start||input.End-input.Start>TimeSpan.FromDays(1)||input.ExpectedCents<0||input.Version!=item.Version||input.ClientId!=item.ClientId)throw new ApiError(409,"Atualize o agendamento antes de editar. Funcionário e conta da cliente são preservados.");
   await using var tx=await db.Database.BeginTransactionAsync();await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Employees\" WHERE \"Id\"={item.EmployeeId} FOR UPDATE");await db.Entry(item).ReloadAsync();if(input.Version!=item.Version)throw new ApiError(409,"Agendamento alterado por outra sessão.");
   item.Title=input.Title.Trim();item.Start=input.Start.ToUniversalTime();item.End=input.End.ToUniversalTime();item.Notes=input.Notes;item.LegacyClient=input.LegacyClient;item.ExpectedCents=input.ExpectedCents;item.DurationMinutes=(int)(input.End-input.Start).TotalMinutes;item.Version=Guid.NewGuid();await db.SaveChangesAsync();await tx.CommitAsync();return Results.Ok(item);
  });
  api.MapGet("/bookings/mine",async(Actor actor,OrbeDb db,int? offset)=>{if(!actor.Client)throw new ApiError(403,"Acesso para clientes.");return Results.Ok(await db.Appointments.Where(x=>x.ClientId==actor.Id).OrderByDescending(x=>x.Start).ThenBy(x=>x.Id).Skip(Math.Max(0,offset??0)).Take(100).Select(x=>new{x.Id,x.Title,x.Status,x.Start,x.End,x.ExpectedCents,x.Version,x.ServiceId,x.EmployeeId,employeeName=db.Employees.Where(e=>e.Id==x.EmployeeId).Select(e=>e.DisplayName).FirstOrDefault()}).ToArrayAsync());}).RequireAuthorization();
  api.MapPost("/bookings",async(ReservationInput input,HttpContext c,Scheduling s)=>Results.Ok(await s.Reserve(input,c.Request.Headers["Idempotency-Key"].ToString()))).RequireAuthorization();
  api.MapPost("/bookings/{id}/cancel",async(string id,[FromBody] VersionInput input,Scheduling s)=>{await s.Cancel(id,input);return Results.NoContent();}).RequireAuthorization();
  api.MapPost("/bookings/{id}/move",async(string id,MoveInput input,Scheduling s)=>Results.Ok(await s.Move(id,input))).RequireAuthorization();
  team.MapDelete("/appointments/{id}",async(string id,[FromBody] VersionInput input,Scheduling s)=>{await s.Delete(id,input);return Results.NoContent();});
  team.MapGet("/entries",async(string? employeeId,int? offset,Actor actor,OrbeDb db)=>{var id=actor.Owner?employeeId:await actor.Employee(employeeId);return Results.Ok(await db.Entries.AsNoTracking().Where(x=>id==null||x.EmployeeId==id).OrderBy(x=>x.Date).ThenBy(x=>x.Id).Skip(Math.Max(0,offset??0)).Take(200).ToArrayAsync());});
  team.MapPost("/entries",async(MoneyInput input,Actor actor,OrbeDb db,Scheduling s)=>
  {
   Text(input.Description,200,"a descrição");if(input.Kind is not ("Receita" or "Despesa")||input.Cents<=0)throw new ApiError(400,"Informe um valor positivo e o tipo do lançamento.");
   var employee=actor.Owner&&input.EmployeeId is null?null:await actor.Employee(input.EmployeeId);
   await using var tx=await db.Database.BeginTransactionAsync();
   if(input.AppointmentId is not null)
   {
    // Lock the event so a linked entry cannot race with cascade deletion.
    await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Appointments\" WHERE \"Id\"={input.AppointmentId} FOR UPDATE");
    var appointment=await s.Accessible(input.AppointmentId,true);
    if(employee is not null&&employee!=appointment.EmployeeId)throw new ApiError(400,"O vínculo pertence a outro funcionário.");employee=appointment.EmployeeId;
   }
   var row=new FinancialEntry{EmployeeId=employee,AppointmentId=input.AppointmentId,Description=input.Description.Trim(),Kind=input.Kind,Date=input.Date,Cents=input.Cents};db.Entries.Add(row);await db.SaveChangesAsync();await tx.CommitAsync();return Results.Ok(row);
  });
  team.MapDelete("/entries/{id}",async(string id,[FromBody] VersionInput input,Actor actor,OrbeDb db)=>{var row=await db.Entries.SingleOrDefaultAsync(x=>x.Id==id)??throw new ApiError(404,"Lançamento não encontrado.");if(!actor.Owner)await actor.Employee(row.EmployeeId??"unassigned");if(row.Version!=input.Version)throw new ApiError(409,"Lançamento alterado. Atualize antes de excluir.");db.Entries.Remove(row);await db.SaveChangesAsync();return Results.NoContent();});
  var owner=api.MapGroup("/owner").RequireAuthorization(p=>p.RequireRole(Roles.Owner));
  owner.MapGet("/employees",async(OrbeDb db)=>Results.Ok(await db.Employees.OrderBy(x=>x.DisplayName).Select(x=>new{x.Id,x.UserId,x.DisplayName,x.Enabled,x.Version,userName=x.User.UserName,email=x.User.Email,services=db.EmployeeServices.Where(e=>e.EmployeeId==x.Id).Select(e=>e.ServiceId).ToArray()}).ToArrayAsync()));
  owner.MapPost("/employees",async(EmployeeInput input,Actor actor,UserManager<AppUser> users,OrbeDb db)=>
  {
   Text(input.UserName,64,"o usuário");Text(input.FullName,150,"o nome");if(!new EmailAddressAttribute().IsValid(input.Email))throw new ApiError(400,"Informe um e-mail válido.");
   await using var tx=await db.Database.BeginTransactionAsync();
   var user=await users.FindByNameAsync(input.UserName);
   if(user is null){user=new(){UserName=input.UserName,Email=input.Email,EmailConfirmed=true,FullName=input.FullName.Trim(),LockoutEnabled=true};AuthService.Check(await users.CreateAsync(user,input.Password));AuthService.Check(await users.AddToRoleAsync(user,Roles.Staff));}
   else if(user.Id!=actor.Id||!actor.Owner)throw new ApiError(400,"Este usuário já existe.");
   if(await db.Employees.AnyAsync(x=>x.UserId==user.Id))throw new ApiError(409,"Usuário já cadastrado como profissional.");
   var row=new Employee{UserId=user.Id,DisplayName=input.FullName.Trim()};db.Employees.Add(row);await db.SaveChangesAsync();await tx.CommitAsync();return Results.Ok(new{row.Id,row.DisplayName,row.Enabled,row.Version});
  });
  owner.MapPut("/employees/{id}",async(string id,EmployeeChange input,Actor actor,UserManager<AppUser> users,OrbeDb db)=>
  {
   Text(input.DisplayName,150,"o nome");if(input.Services is null||input.Services.Length>200||input.Services.Distinct().Count()!=input.Services.Length)throw new ApiError(400,"Lista de serviços inválida.");
   await using var tx=await db.Database.BeginTransactionAsync();await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Employees\" WHERE \"Id\"={id} FOR UPDATE");
   var employee=await db.Employees.SingleOrDefaultAsync(x=>x.Id==id)??throw new ApiError(404,"Funcionário não encontrado.");if(employee.Version!=input.Version)throw new ApiError(409,"Funcionário alterado. Atualize a lista.");
   if(await db.Services.CountAsync(x=>input.Services.Contains(x.Id))!=input.Services.Length)throw new ApiError(400,"Serviço inválido.");
   employee.DisplayName=input.DisplayName.Trim();employee.Enabled=input.Enabled;employee.Version=Guid.NewGuid();
   var user=(await users.FindByIdAsync(employee.UserId))!;if(!await users.IsInRoleAsync(user,Roles.Owner)){user.Active=input.Enabled;AuthService.Check(await users.UpdateAsync(user));if(!input.Enabled)await users.UpdateSecurityStampAsync(user);}
   await db.EmployeeServices.Where(x=>x.EmployeeId==id).ExecuteDeleteAsync();db.EmployeeServices.AddRange(input.Services.Select(s=>new EmployeeService{EmployeeId=id,ServiceId=s}));await db.SaveChangesAsync();await tx.CommitAsync();return Results.Ok(new{employee.Id,employee.Version});
  });
  owner.MapPost("/employees/{id}/password",async(string id,ResetStaffInput input,OrbeDb db,UserManager<AppUser> users)=>{var employee=await db.Employees.SingleOrDefaultAsync(x=>x.Id==id)??throw new ApiError(404,"Funcionário não encontrado.");var user=(await users.FindByIdAsync(employee.UserId))!;if(await users.IsInRoleAsync(user,Roles.Owner))throw new ApiError(403,"Use a troca de senha da própria conta.");AuthService.Check(await users.ResetPasswordAsync(user,await users.GeneratePasswordResetTokenAsync(user),input.Password));return Results.Ok(new{message="Senha redefinida; sessões anteriores encerradas."});});
  owner.MapGet("/services",async(OrbeDb db)=>Results.Ok(await db.Services.OrderBy(x=>x.Name).ToArrayAsync()));
  owner.MapPost("/services",async(ServiceInput input,OrbeDb db)=>{ValidateService(input);var row=new Service{Name=input.Name.Trim(),DurationMinutes=input.DurationMinutes,PriceCents=input.PriceCents,Enabled=input.Enabled};db.Services.Add(row);await db.SaveChangesAsync();return Results.Ok(row);});
  owner.MapPut("/services/{id}",async(string id,ServiceInput input,OrbeDb db)=>{ValidateService(input);var row=await db.Services.SingleOrDefaultAsync(x=>x.Id==id)??throw new ApiError(404,"Serviço não encontrado.");if(row.Version!=input.Version)throw new ApiError(409,"Serviço alterado. Atualize a lista.");row.Name=input.Name.Trim();row.DurationMinutes=input.DurationMinutes;row.PriceCents=input.PriceCents;row.Enabled=input.Enabled;row.Version=Guid.NewGuid();await db.SaveChangesAsync();return Results.Ok(row);});
  owner.MapGet("/settings",async(OrbeDb db)=>Results.Ok(await db.Settings.SingleAsync()));
  owner.MapPut("/settings",async(PolicyInput input,OrbeDb db)=>
  {
   Text(input.Name,100,"o nome do estabelecimento");var row=await db.Settings.SingleAsync();if(row.Version!=input.Version)throw new ApiError(409,"Configurações alteradas. Atualize.");
   new Orbe.Cloud.Core.BookingPolicy(input.SlotMinutes,input.LeadMinutes,input.HorizonDays,input.CancellationHours,input.TimeZoneId).Validate();
   if(input.PublicBookingOpen)
   {
    if(!input.ImportCompleted||await db.Appointments.AnyAsync(x=>x.EmployeeId==null))throw new ApiError(409,"Conclua a importação e atribua os agendamentos antigos antes de abrir reservas.");
    if(!await db.EmployeeServices.AnyAsync(x=>db.Employees.Any(e=>e.Id==x.EmployeeId&&e.Enabled&&e.User.Active)&&db.Services.Any(s=>s.Id==x.ServiceId&&s.Enabled)&&db.WeekWindows.Any(w=>w.EmployeeId==x.EmployeeId)))throw new ApiError(409,"Configure ao menos um profissional ativo, serviço e jornada antes de abrir reservas.");
   }
   row.Name=input.Name.Trim();row.ImportCompleted=input.ImportCompleted;row.PublicBookingOpen=input.PublicBookingOpen;row.SlotMinutes=input.SlotMinutes;row.LeadMinutes=input.LeadMinutes;row.HorizonDays=input.HorizonDays;row.CancellationHours=input.CancellationHours;row.TimeZoneId=input.TimeZoneId;row.Version=Guid.NewGuid();await db.SaveChangesAsync();return Results.Ok(row);
  });
 }
 private static void ValidateService(ServiceInput input){Text(input.Name,150,"o nome do serviço");if(input.DurationMinutes is <1 or >1440||input.PriceCents<0)throw new ApiError(400,"Confira duração e preço do serviço.");}
}
