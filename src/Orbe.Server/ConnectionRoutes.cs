using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Orbe.Cloud.Core;

namespace Orbe.Server;
public sealed record SharedConnectionInput(Guid Version,string ApiUrl);
public interface IConnectionProbe { Task<Guid> Instance(string url,CancellationToken token); }
public sealed class ConnectionProbe(HttpClient http):IConnectionProbe
{
 public async Task<Guid> Instance(string url,CancellationToken token)
 {
  try
  {
   using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(8));
   using var response=await http.GetAsync(url+"/api/v1/public/connection",HttpCompletionOption.ResponseHeadersRead,timeout.Token);response.EnsureSuccessStatusCode();
   if(response.Content.Headers.ContentLength>16384)throw new InvalidDataException();
   await using var stream=await response.Content.ReadAsStreamAsync(timeout.Token);using var memory=new MemoryStream();var buffer=new byte[4096];int read;
   while((read=await stream.ReadAsync(buffer,timeout.Token))>0){if(memory.Length+read>16384)throw new InvalidDataException();memory.Write(buffer,0,read);}
   var result=System.Text.Json.JsonSerializer.Deserialize<SharedConnection>(memory.ToArray(),new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
   return result?.InstanceId??Guid.Empty;
  }
  catch(Exception e)when(e is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or InvalidDataException){throw new ApiError(409,"Não foi possível validar a aplicação de destino. A conexão atual foi mantida.");}
 }
}
public static class ConnectionRoutes
{
 public static void MapSharedConnection(this WebApplication app)
 {
  var testing=app.Environment.IsEnvironment("Testing");
  SharedConnection Info(BusinessSettings s)=>new(ConnectionAddress.Normalize(s.ApiBaseUrl??app.Configuration["PublicUrl"],testing),s.InstanceId,s.Version);
  app.MapGet("/api/v1/public/connection",async(OrbeDb db)=>Results.Ok(Info(await db.Settings.AsNoTracking().SingleAsync())));
  app.MapPut("/api/v1/owner/connection",async(SharedConnectionInput input,OrbeDb db,IConnectionProbe probe,HttpContext context)=>
  {
   var setting=await db.Settings.SingleAsync();if(setting.Version!=input.Version)throw new ApiError(409,"Configuração alterada. Atualize antes de salvar.");
   string url;try{url=ConnectionAddress.Normalize(input.ApiUrl,testing);}catch(ArgumentException e){throw new ApiError(400,e.Message);}
   var current=Info(setting);
   if(url!=current.ApiUrl&&await probe.Instance(url,context.RequestAborted)!=setting.InstanceId)throw new ApiError(409,"O destino não usa o mesmo banco Orbe. Contas e dados precisam estar disponíveis antes de mudar o endereço.");
   setting.ApiBaseUrl=url;setting.Version=Guid.NewGuid();await db.SaveChangesAsync();return Results.Ok(Info(setting));
  }).RequireAuthorization(p=>p.RequireRole(Roles.Owner)).RequireRateLimiting("account");
 }
}
