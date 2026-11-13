using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;

namespace Orbe.Server;

public sealed class PushDevice
{
 public string Token {get;set;}="";
 public string SessionHash {get;set;}="";
 public DateTimeOffset RegisteredAt {get;set;}
}

public sealed class PushReceipt
{
 public string Token {get;set;}="";
 public Guid NoticeId {get;set;}
}

public sealed record PushRegistration(string Token);

public static class PushRoutes
{
 public static void MapPush(this WebApplication app)
 {
  app.MapPut("/api/v1/team/push-device",async(PushRegistration input,HttpContext context,OrbeDb db,TimeProvider clock,IConfiguration config)=>{
   if(string.IsNullOrWhiteSpace(config["Push:ServiceAccountJson"]))return Results.NoContent();
   var header=context.Request.Headers.Authorization.ToString();
   if(!header.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase))return Results.Forbid();
   if(input.Token is not {Length:>=20 and <=512}||input.Token.Any(char.IsWhiteSpace))throw new ApiError(400,"Dispositivo inválido.");
   var session=Secrets.Hash(header[7..]);
   var now=clock.GetUtcNow();
   await db.Database.ExecuteSqlInterpolatedAsync($"""
    INSERT INTO "PushDevices" ("Token","SessionHash","RegisteredAt") VALUES ({input.Token},{session},{now})
    ON CONFLICT ("Token") DO UPDATE SET "SessionHash"=EXCLUDED."SessionHash",
     "RegisteredAt"=CASE WHEN "PushDevices"."SessionHash"=EXCLUDED."SessionHash" THEN "PushDevices"."RegisteredAt" ELSE EXCLUDED."RegisteredAt" END
    """);
   return Results.NoContent();
  }).RequireAuthorization(p=>p.RequireRole(Roles.Owner,Roles.Staff)).RequireRateLimiting("account");
 }
}

public sealed class PushWorker(IServiceScopeFactory scopes,IConfiguration config,IHttpClientFactory clients,TimeProvider clock,ILogger<PushWorker> log):BackgroundService
{
 protected override async Task ExecuteAsync(CancellationToken stoppingToken)
 {
  var json=config["Push:ServiceAccountJson"]!;
  var credential=CredentialFactory.FromJson<ServiceAccountCredential>(json);
  var scoped=credential.ToGoogleCredential().CreateScoped("https://www.googleapis.com/auth/firebase.messaging");
  var endpoint=$"https://fcm.googleapis.com/v1/projects/{Uri.EscapeDataString(credential.ProjectId)}/messages:send";
  using var timer=new PeriodicTimer(TimeSpan.FromSeconds(30));
  do
  {
   try{await Dispatch(scoped,endpoint,stoppingToken);}
   catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){return;}
   catch(Exception e){log.LogWarning("Falha no ciclo de avisos Android ({Type}).",e.GetType().Name);}
  }while(await timer.WaitForNextTickAsync(stoppingToken));
 }

 private async Task Dispatch(GoogleCredential credential,string endpoint,CancellationToken cancellation)
 {
  using var scope=scopes.CreateScope();
  var db=scope.ServiceProvider.GetRequiredService<OrbeDb>();
  var inbox=scope.ServiceProvider.GetRequiredService<NotificationInbox>();
  var now=clock.GetUtcNow();
  var devices=from d in db.Set<PushDevice>() join s in db.Sessions on d.SessionHash equals s.TokenHash
   join u in db.Users on s.UserId equals u.Id
   where s.ExpiresAt>now&&u.Active&&u.EmailConfirmed&&u.SecurityStamp==s.SecurityStamp
   select new{d.Token,d.RegisteredAt,u.Id,Owner=db.UserRoles.Any(ur=>ur.UserId==u.Id&&db.Roles.Any(r=>r.Id==ur.RoleId&&r.Name==Roles.Owner))};
  var active=await devices.ToArrayAsync(cancellation);
  using var http=clients.CreateClient();
  foreach(var user in active.GroupBy(x=>new{x.Id,x.Owner}))
  {
   await inbox.Materialize(user.Key.Id,user.Key.Owner);
   foreach(var device in user)
   {
    var earliest=device.RegisteredAt>now.AddDays(-2)?device.RegisteredAt:now.AddDays(-2);
    var due=await (from n in db.Notifications join a in db.Appointments on n.AppointmentId equals a.Id
     where n.UserId==device.Id&&n.DeliverAt>=earliest&&n.DeliverAt<=now&&n.DeletedAt==null&&n.ReadAt==null&&
      (n.Kind=="Review"?a.Status=="Pending":a.Status=="Confirmed")&&a.Start==n.OccurrenceStart&&(n.Kind=="Booking"||n.Kind=="Review"||a.Start>now)&&
      (device.Owner||db.Employees.Any(e=>e.Id==a.EmployeeId&&e.UserId==device.Id&&e.Enabled))&&
      !db.Set<PushReceipt>().Any(r=>r.Token==device.Token&&r.NoticeId==n.Id)
     orderby n.DeliverAt,n.Id select new{n.Id,n.Kind}).Take(50).ToArrayAsync(cancellation);
    foreach(var notice in due)
    {
     using var request=new HttpRequestMessage(HttpMethod.Post,endpoint);
     request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",await credential.UnderlyingCredential.GetAccessTokenForRequestAsync(cancellationToken:cancellation));
     request.Content=JsonContent.Create(new{message=new{token=device.Token,
      notification=new{title="Orbe",body=notice.Kind switch{"Booking"=>"Você tem um novo agendamento.","Review"=>"Um agendamento fixo precisa ser remarcado.",_=>"Você tem um lembrete de agendamento."}},
      data=new{orbe_notifications="1",notice_id=notice.Id.ToString()},
      android=new{priority="HIGH",notification=new{tag=notice.Id.ToString(),channel_id="orbe-inbox"}}}});
     using var response=await http.SendAsync(request,cancellation);
     if(response.IsSuccessStatusCode)
     {
      await db.Database.ExecuteSqlInterpolatedAsync($"""
       INSERT INTO "PushReceipts" ("Token","NoticeId") SELECT {device.Token},{notice.Id}
       WHERE EXISTS(SELECT 1 FROM "PushDevices" WHERE "Token"={device.Token})
        AND EXISTS(SELECT 1 FROM "Notifications" WHERE "Id"={notice.Id})
       ON CONFLICT DO NOTHING
       """,cancellation);
     }
     else if(response.StatusCode==HttpStatusCode.NotFound)
     {
      using var body=JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellation));
      if(body.RootElement.TryGetProperty("error",out var error)&&error.TryGetProperty("details",out var details)&&
       details.EnumerateArray().Any(d=>d.TryGetProperty("errorCode",out var code)&&code.GetString()=="UNREGISTERED"))
       await db.Set<PushDevice>().Where(d=>d.Token==device.Token).ExecuteDeleteAsync(cancellation);
      break;
     }
     else{log.LogWarning("Firebase respondeu com status {Status}.",response.StatusCode);break;}
    }
   }
  }
 }
}
