using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Orbe.Server;

public static class Bootstrap
{
 public static async Task Initialize(IServiceProvider services,IConfiguration configuration)
 {
  using var scope=services.CreateScope();
  var db=scope.ServiceProvider.GetRequiredService<OrbeDb>();
  await db.Database.MigrateAsync();
  var roles=scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
  var users=scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
  foreach(var role in new[]{Roles.Owner,Roles.Staff,Roles.Client})
   if(!await roles.RoleExistsAsync(role))AuthService.Check(await roles.CreateAsync(new(role)));
  if(!await db.Settings.AnyAsync()){db.Settings.Add(new());await db.SaveChangesAsync();}
  if((await users.GetUsersInRoleAsync(Roles.Owner)).Count==0)
  {
   var password=configuration["Owner:Password"];var address=configuration["Owner:Email"];
   if(string.IsNullOrWhiteSpace(password)||string.IsNullOrWhiteSpace(address))
   {
    if(configuration.GetValue<bool>("Bootstrap:AllowUnconfiguredOwner"))return; // Closed preview; grants no access and exposes no bootstrap route.
    throw new InvalidOperationException("Configure os segredos Owner:Email e Owner:Password para o primeiro acesso. Nenhuma senha padrão é criada.");
   }
   if(await users.FindByNameAsync("WillianBN") is not null)throw new InvalidOperationException("O nome administrativo já existe sem papel Owner. Corrija o bootstrap em ambiente controlado.");
   await using var tx=await db.Database.BeginTransactionAsync();
   var user=new AppUser{UserName="WillianBN",Email=address,EmailConfirmed=true,FullName="WillianBN",LockoutEnabled=true};
   AuthService.Check(await users.CreateAsync(user,password));AuthService.Check(await users.AddToRoleAsync(user,Roles.Owner));
   await tx.CommitAsync();
  }
 }
}
