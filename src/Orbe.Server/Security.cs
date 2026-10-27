using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Orbe.Server;

public sealed class ApiError(int status, string message, object? details=null) : Exception(message) { public int Status {get;}=status; public object? Details {get;}=details; }
public static class Secrets
{
 public const string Cookie="Orbe.Session";
 public static string Hash(string value)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
 public static string Random()=>WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
 public static CookieOptions CookieOptions(HttpContext c,DateTimeOffset? expiry=null)=>new(){HttpOnly=true,Secure=!c.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment()&&!c.RequestServices.GetRequiredService<IHostEnvironment>().IsEnvironment("Testing"),SameSite=SameSiteMode.Strict,Path="/",Expires=expiry};
}
public sealed class SessionAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
 OrbeDb db, UserManager<AppUser> users,TimeProvider clock,IConfiguration config) : AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
{
 protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
 {
  var header=Request.Headers.Authorization.ToString();
  var token=header.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase)?header[7..]:Request.Cookies[Secrets.Cookie];
  if(string.IsNullOrEmpty(token)||token.Length>256)return AuthenticateResult.NoResult();
  var hash=Secrets.Hash(token);var now=clock.GetUtcNow();
  var session=await db.Sessions.AsNoTracking().SingleOrDefaultAsync(x=>x.TokenHash==hash&&x.ExpiresAt>now);
  if(session is null)return AuthenticateResult.Fail("Sessão inválida.");
  var user=await users.FindByIdAsync(session.UserId);
  if(user is null||!user.Active||user.SecurityStamp!=session.SecurityStamp)return AuthenticateResult.Fail("Sessão encerrada.");
  if(!user.EmailConfirmed&&!(config.GetValue<bool>("Preview:AllowUnverifiedClients")&&(await users.GetRolesAsync(user)).SequenceEqual(new[]{Roles.Client})))return AuthenticateResult.Fail("Confirme seu e-mail.");
  var claims=new List<Claim>{new(ClaimTypes.NameIdentifier,user.Id),new(ClaimTypes.Name,user.UserName??user.FullName)};
  claims.AddRange((await users.GetRolesAsync(user)).Select(role=>new Claim(ClaimTypes.Role,role)));
  return AuthenticateResult.Success(new(new ClaimsPrincipal(new ClaimsIdentity(claims,Scheme.Name)),Scheme.Name));
 }
}
public sealed class Actor(IHttpContextAccessor accessor,OrbeDb db)
{
 public ClaimsPrincipal User=>accessor.HttpContext?.User??new ClaimsPrincipal();
 public string Id=>User.FindFirstValue(ClaimTypes.NameIdentifier)??throw new ApiError(401,"Entre na sua conta.");
 public bool Owner=>User.IsInRole(Roles.Owner);
 public bool Client=>User.IsInRole(Roles.Client);
 public void RequireOwner(){if(!Owner)throw new ApiError(403,"Esta ação é exclusiva de WillianBN.");}
 public void RequireTeam(){if(!Owner&&!User.IsInRole(Roles.Staff))throw new ApiError(403,"Acesso exclusivo da equipe.");}
 public async Task<string> Employee(string? requested,bool allowInactive=false)
 {
  RequireTeam();
  if(Owner){if(string.IsNullOrWhiteSpace(requested)||!await db.Employees.AnyAsync(x=>x.Id==requested&&(allowInactive||x.Enabled)))throw new ApiError(400,allowInactive?"Selecione um funcionário válido.":"Selecione um funcionário ativo.");return requested;}
  var own=await db.Employees.SingleOrDefaultAsync(x=>x.UserId==Id&&x.Enabled)??throw new ApiError(403,"Funcionário indisponível.");
  if(requested is not null&&requested!=own.Id)throw new ApiError(403,"Você só pode acessar a própria agenda.");
  return own.Id;
 }
}
public interface IEmailDelivery { bool Ready {get;} Task Send(string to,string subject,string body); }
public sealed class SmtpEmail(IConfiguration config):IEmailDelivery
{
 public bool Ready=>!string.IsNullOrWhiteSpace(config["Smtp:Host"])&&!string.IsNullOrWhiteSpace(config["Smtp:From"]);
 public async Task Send(string to,string subject,string body)
 {
  if(!Ready)throw new ApiError(503,"O envio de e-mails ainda não foi configurado. Entre em contato com a equipe.");
  using var message=new MailMessage(config["Smtp:From"]!,to,subject,body){IsBodyHtml=false};
  using var smtp=new SmtpClient(config["Smtp:Host"],config.GetValue("Smtp:Port",587)){EnableSsl=true,Timeout=15000};
  if(!string.IsNullOrEmpty(config["Smtp:User"]))smtp.Credentials=new NetworkCredential(config["Smtp:User"],config["Smtp:Password"]);
  try{await smtp.SendMailAsync(message);}catch(SmtpException){throw new ApiError(503,"Não foi possível enviar o e-mail agora. Tente novamente mais tarde.");}
 }
}
public sealed class AuthService(OrbeDb db,UserManager<AppUser> users,IEmailDelivery email,IConfiguration config,TimeProvider clock)
{
 public async Task<object> Profile(AppUser u)=>new{u.Id,u.FullName,u.Email,phone=u.PhoneNumber,u.BirthDate,u.EmailConfirmed,roles=await users.GetRolesAsync(u),employeeId=await db.Employees.Where(x=>x.UserId==u.Id&&x.Enabled).Select(x=>x.Id).SingleOrDefaultAsync()};
 public static void Check(IdentityResult result)
 {if(!result.Succeeded)throw new ApiError(400,string.Join(" ",result.Errors.Select(x=>x.Code switch{"DuplicateUserName" or "DuplicateEmail"=>"Já existe uma conta com esses dados.",_=>"Confira os dados. A senha deve ter pelo menos 10 caracteres, letras maiúsculas/minúsculas e números."})));}
 private string Link(string path,string userId,string token,string? newEmail=null)
 {
  var root=config["PublicUrl"]?.TrimEnd('/');
  if(!Uri.TryCreate(root,UriKind.Absolute,out var uri)||uri.Scheme!="https"&&uri.Host!="localhost"&&uri.Host!="127.0.0.1")throw new ApiError(503,"Endereço público do Orbe não configurado.");
  return $"{root}/{path}?userId={Uri.EscapeDataString(userId)}&token={Uri.EscapeDataString(token)}"+(newEmail is null?"":"&email="+Uri.EscapeDataString(newEmail));
 }
 public async Task ConfirmEmail(AppUser user)=>await email.Send(user.Email!,"Confirme sua conta Orbe","Confirme seu e-mail: "+Link("confirmar-email",user.Id,await users.GenerateEmailConfirmationTokenAsync(user)));
 public async Task<(string Token,AppUser User,DateTimeOffset Expiry)> Login(string login,string password)
 {
  if(string.IsNullOrWhiteSpace(login)||login.Length>256||string.IsNullOrEmpty(password)||password.Length>256)throw new ApiError(401,"Usuário ou senha inválidos.");
  var user=await users.FindByNameAsync(login)??await users.FindByEmailAsync(login);
  if(user is null||!user.Active||await users.IsLockedOutAsync(user))throw new ApiError(401,"Usuário ou senha inválidos. Se necessário, aguarde e tente novamente.");
  if(!await users.CheckPasswordAsync(user,password)){await users.AccessFailedAsync(user);throw new ApiError(401,"Usuário ou senha inválidos. Se necessário, aguarde e tente novamente.");}
  if(!user.EmailConfirmed&&!(config.GetValue<bool>("Preview:AllowUnverifiedClients")&&(await users.GetRolesAsync(user)).SequenceEqual(new[]{Roles.Client})))throw new ApiError(403,"Confirme seu e-mail antes de entrar.");
  await users.ResetAccessFailedCountAsync(user);
  var token=Secrets.Random();var expiry=clock.GetUtcNow().AddDays(14);
  db.Sessions.Add(new(){TokenHash=Secrets.Hash(token),UserId=user.Id,SecurityStamp=user.SecurityStamp!,ExpiresAt=expiry});
  await db.SaveChangesAsync();return(token,user,expiry);
 }
 public async Task Recover(string address)
 {
  if(!email.Ready)throw new ApiError(503,"O envio de e-mails ainda não foi configurado. Entre em contato com a equipe.");
  var user=await users.FindByEmailAsync(address);
  if(user is not null&&user.Active&&user.EmailConfirmed)
   await email.Send(user.Email!,"Recuperar acesso ao Orbe","Defina uma nova senha: "+Link("redefinir-senha",user.Id,await users.GeneratePasswordResetTokenAsync(user)));
 }
 public async Task ChangeEmail(AppUser user,string next,string password)
 {
  if(!await users.CheckPasswordAsync(user,password))throw new ApiError(400,"Confira sua senha atual.");
  if(await users.FindByEmailAsync(next) is { } existing&&existing.Id!=user.Id)throw new ApiError(400,"E-mail indisponível.");
  await email.Send(next,"Alterar e-mail no Orbe","Confirme seu novo e-mail: "+Link("confirmar-email",user.Id,await users.GenerateChangeEmailTokenAsync(user,next),next));
 }
}
public sealed record RegisterInput(string FullName,string Email,string Phone,DateOnly BirthDate,string Password);
public sealed record LoginInput(string Login,string Password);
public sealed record EmailInput(string Email);
public sealed record TokenInput(string UserId,string Token,string Password);
public sealed record ProfileInput(string FullName,string Phone,DateOnly? BirthDate);
public sealed record PasswordInput(string CurrentPassword,string NewPassword);
public sealed record ChangeEmailInput(string Email,string Password);

public static class AuthRoutes
{
 public static void MapAuth(this WebApplication app)
 {
  var group=app.MapGroup("/api/v1/auth").RequireRateLimiting("auth");
  group.MapGet("/csrf",(HttpContext c,IAntiforgery anti)=>Results.Ok(new{token=anti.GetAndStoreTokens(c).RequestToken})).RequireRateLimiting("account");
  group.MapPost("/register",async(RegisterInput input,UserManager<AppUser> users,AuthService auth,IEmailDelivery email,IConfiguration config)=>
  {
   var preview=config.GetValue<bool>("Preview:AllowUnverifiedClients");
   if(!email.Ready&&!preview)throw new ApiError(503,"Cadastro disponível após configurar o envio de e-mails.");
   if(string.IsNullOrWhiteSpace(input.FullName)||input.FullName.Length>150||string.IsNullOrWhiteSpace(input.Phone)||input.Phone.Length>30||new string(input.Phone.Where(char.IsDigit).ToArray()).Length<8||string.IsNullOrWhiteSpace(input.Email)||input.Email.Length>256||input.Password is null||input.Password.Length>256||input.BirthDate>DateOnly.FromDateTime(DateTime.UtcNow)||input.BirthDate.Year<1900)throw new ApiError(400,"Confira nome, telefone, e-mail e nascimento. Use senha de 10 a 256 caracteres.");
   if(await users.FindByEmailAsync(input.Email) is null)
   {
    var user=new AppUser{UserName=input.Email,Email=input.Email,FullName=input.FullName.Trim(),PhoneNumber=input.Phone,BirthDate=input.BirthDate,LockoutEnabled=true};
    AuthService.Check(await users.CreateAsync(user,input.Password));AuthService.Check(await users.AddToRoleAsync(user,Roles.Client));if(!preview)await auth.ConfirmEmail(user);
   }
   return Results.Accepted(value:new{emailConfirmationRequired=!preview,message=preview?"Ambiente de testes: se o cadastro foi concluído, você já pode entrar. O e-mail ainda não foi verificado.":"Confira seu e-mail para confirmar o cadastro."});
  });
  group.MapPost("/resend",async(EmailInput input,UserManager<AppUser> users,AuthService auth)=>{var user=await users.FindByEmailAsync(input.Email);if(user is not null&&!user.EmailConfirmed&&user.Active)await auth.ConfirmEmail(user);return Results.Ok(new{message="Se houver uma confirmação pendente, enviaremos as instruções."});});
  group.MapPost("/login",async(LoginInput input,AuthService auth,HttpContext c,IAntiforgery anti)=>
  {await anti.ValidateRequestAsync(c);var result=await auth.Login(input.Login,input.Password);c.Response.Cookies.Append(Secrets.Cookie,result.Token,Secrets.CookieOptions(c,result.Expiry));return Results.Ok(await auth.Profile(result.User));});
  group.MapPost("/token",async(LoginInput input,AuthService auth)=>{var result=await auth.Login(input.Login,input.Password);return Results.Ok(new{accessToken=result.Token,expiresAt=result.Expiry,profile=await auth.Profile(result.User)});});
  group.MapPost("/recover",async(EmailInput input,AuthService auth)=>{await auth.Recover(input.Email);return Results.Ok(new{message="Se o e-mail estiver cadastrado e confirmado, enviaremos as instruções."});});
  group.MapPost("/reset",async(TokenInput input,UserManager<AppUser> users)=>{var user=await users.FindByIdAsync(input.UserId)??throw new ApiError(400,"Link inválido ou expirado.");AuthService.Check(await users.ResetPasswordAsync(user,input.Token,input.Password));return Results.Ok(new{message="Senha alterada. Entre novamente."});});
  group.MapPost("/confirm",async(string userId,string token,string? email,UserManager<AppUser> users)=>
  {
   var user=await users.FindByIdAsync(userId)??throw new ApiError(400,"Link inválido ou expirado.");
   if(email is null)AuthService.Check(await users.ConfirmEmailAsync(user,token));
   else{AuthService.Check(await users.ChangeEmailAsync(user,email,token));if(await users.IsInRoleAsync(user,Roles.Client))AuthService.Check(await users.SetUserNameAsync(user,email));await users.UpdateSecurityStampAsync(user);}
   return Results.Ok(new{message="E-mail confirmado. Você já pode entrar."});
  });
  group.MapGet("/me",async(Actor actor,UserManager<AppUser> users,AuthService auth)=>Results.Ok(await auth.Profile((await users.FindByIdAsync(actor.Id))!))).RequireAuthorization().RequireRateLimiting("account");
  group.MapPut("/me",async(ProfileInput input,Actor actor,UserManager<AppUser> users,AuthService auth)=>
  {if(string.IsNullOrWhiteSpace(input.FullName)||input.FullName.Length>150||input.Phone is null||input.Phone.Length>30||input.BirthDate>DateOnly.FromDateTime(DateTime.UtcNow)||actor.Client&&(input.BirthDate is null||input.BirthDate.Value.Year<1900||new string(input.Phone.Where(char.IsDigit).ToArray()).Length<8))throw new ApiError(400,"Confira seus dados.");var user=(await users.FindByIdAsync(actor.Id))!;user.FullName=input.FullName.Trim();user.PhoneNumber=input.Phone;user.BirthDate=input.BirthDate;AuthService.Check(await users.UpdateAsync(user));return Results.Ok(await auth.Profile(user));}).RequireAuthorization();
  group.MapPost("/password",async(PasswordInput input,Actor actor,UserManager<AppUser> users)=>{var user=(await users.FindByIdAsync(actor.Id))!;AuthService.Check(await users.ChangePasswordAsync(user,input.CurrentPassword,input.NewPassword));return Results.Ok(new{message="Senha alterada. Entre novamente."});}).RequireAuthorization();
  group.MapPost("/email",async(ChangeEmailInput input,Actor actor,UserManager<AppUser> users,AuthService auth)=>{await auth.ChangeEmail((await users.FindByIdAsync(actor.Id))!,input.Email,input.Password);return Results.Ok(new{message="Confirme o novo endereço pelo e-mail enviado."});}).RequireAuthorization();
  group.MapPost("/logout",async(HttpContext c,OrbeDb db)=>{var header=c.Request.Headers.Authorization.ToString();var token=header.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase)?header[7..]:c.Request.Cookies[Secrets.Cookie];if(token is not null)await db.Sessions.Where(x=>x.TokenHash==Secrets.Hash(token)).ExecuteDeleteAsync();c.Response.Cookies.Delete(Secrets.Cookie,Secrets.CookieOptions(c));return Results.NoContent();}).RequireAuthorization();
 }
}
