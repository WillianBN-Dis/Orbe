using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Orbe.Server;

namespace Orbe.Server.Checks;

internal static class IdentityChecks
{
 public static async Task Verify()
 {
  var services=new ServiceCollection();services.AddLogging();
  services.AddIdentityCore<AppUser>(o=>{o.Password.RequiredLength=10;o.Password.RequireNonAlphanumeric=false;o.User.RequireUniqueEmail=true;}).AddUserStore<MemoryUsers>();
  using var provider=services.BuildServiceProvider();using var scope=provider.CreateScope();
  var users=scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
  var password=Environment.GetEnvironmentVariable("ORBE_QA_STAFF_PASSWORD")??"483927Senha.";
  var user=new AppUser{UserName="teste.funcionario",Email="funcionario@example.test",FullName="Nome Teste"};
  AuthService.Check(await users.CreateAsync(user,password));
  if(!await users.CheckPasswordAsync(user,password))throw new Exception("New employee password did not authenticate");
  Console.WriteLine("PASS: usuário teste.funcionario, nome completo e senha válida criam acesso e autenticam em memória");
  async Task Reject(string name,string email,string candidate,string expected)
  {
   var result=await users.CreateAsync(new(){UserName=name,Email=email,FullName="Nome Teste"},candidate);
   if(result.Succeeded)throw new Exception("Invalid identity accepted");
   try{AuthService.Check(result);throw new Exception("Missing validation error");}
   catch(ApiError e){if(!e.Message.Contains(expected))throw new Exception("Wrong identity validation message");}
  }
  await Reject("Nome Teste","space@example.test",password,"Não use espaços");
  await Reject("funcionário","accent@example.test",password,"sem acentos");
  await Reject("senha.curta","short@example.test","Abc1","10 caracteres");
  await Reject("email.repetido","funcionario@example.test",password,"e-mail já está associado");
  await Reject("teste.funcionario","different@example.test",password,"usuário já está em uso");
  Console.WriteLine("PASS: espaço/acento no usuário, senha curta, e-mail e usuário repetidos recebem mensagens específicas");
 }
 // Only the real Identity validators/hashers are exercised; no database, API or real account is accessed.
 private sealed class MemoryUsers:IUserPasswordStore<AppUser>,IUserEmailStore<AppUser>
 {
  private readonly List<AppUser> rows=[];
  public void Dispose(){}
  public Task<IdentityResult> CreateAsync(AppUser user,CancellationToken ct){rows.Add(user);return Task.FromResult(IdentityResult.Success);}
  public Task<IdentityResult> UpdateAsync(AppUser user,CancellationToken ct)=>Task.FromResult(IdentityResult.Success);
  public Task<IdentityResult> DeleteAsync(AppUser user,CancellationToken ct){rows.Remove(user);return Task.FromResult(IdentityResult.Success);}
  public Task<AppUser?> FindByIdAsync(string id,CancellationToken ct)=>Task.FromResult(rows.FirstOrDefault(x=>x.Id==id));
  public Task<AppUser?> FindByNameAsync(string name,CancellationToken ct)=>Task.FromResult(rows.FirstOrDefault(x=>x.NormalizedUserName==name));
  public Task<string> GetUserIdAsync(AppUser user,CancellationToken ct)=>Task.FromResult(user.Id);
  public Task<string?> GetUserNameAsync(AppUser user,CancellationToken ct)=>Task.FromResult(user.UserName);
  public Task SetUserNameAsync(AppUser user,string? name,CancellationToken ct){user.UserName=name;return Task.CompletedTask;}
  public Task<string?> GetNormalizedUserNameAsync(AppUser user,CancellationToken ct)=>Task.FromResult(user.NormalizedUserName);
  public Task SetNormalizedUserNameAsync(AppUser user,string? name,CancellationToken ct){user.NormalizedUserName=name;return Task.CompletedTask;}
  public Task SetPasswordHashAsync(AppUser user,string? hash,CancellationToken ct){user.PasswordHash=hash;return Task.CompletedTask;}
  public Task<string?> GetPasswordHashAsync(AppUser user,CancellationToken ct)=>Task.FromResult(user.PasswordHash);
  public Task<bool> HasPasswordAsync(AppUser user,CancellationToken ct)=>Task.FromResult(user.PasswordHash is not null);
  public Task SetEmailAsync(AppUser user,string? email,CancellationToken ct){user.Email=email;return Task.CompletedTask;}
  public Task<string?> GetEmailAsync(AppUser user,CancellationToken ct)=>Task.FromResult(user.Email);
  public Task<bool> GetEmailConfirmedAsync(AppUser user,CancellationToken ct)=>Task.FromResult(user.EmailConfirmed);
  public Task SetEmailConfirmedAsync(AppUser user,bool value,CancellationToken ct){user.EmailConfirmed=value;return Task.CompletedTask;}
  public Task<AppUser?> FindByEmailAsync(string email,CancellationToken ct)=>Task.FromResult(rows.FirstOrDefault(x=>x.NormalizedEmail==email));
  public Task<string?> GetNormalizedEmailAsync(AppUser user,CancellationToken ct)=>Task.FromResult(user.NormalizedEmail);
  public Task SetNormalizedEmailAsync(AppUser user,string? email,CancellationToken ct){user.NormalizedEmail=email;return Task.CompletedTask;}
 }
}
