using System.Text.Json;
using Orbe.Cloud.Core;

namespace Agenda.Core;
public static class CloudRouting
{
 public static async Task<SharedConnection> Discover(HttpMessageHandler? handler=null,string bootstrap=CloudApi.DefaultUrl,CancellationToken token=default)
 {
  using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromSeconds(10));token=deadline.Token;
  var local=new Uri(bootstrap).IsLoopback;bootstrap=ConnectionAddress.Normalize(bootstrap,local);
  using var http=handler is null?new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}):new HttpClient(handler);
  http.Timeout=TimeSpan.FromSeconds(10);
  using var request=new HttpRequestMessage(HttpMethod.Get,bootstrap+"/api/v1/public/connection");request.Headers.CacheControl=new(){NoCache=true};
  using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);response.EnsureSuccessStatusCode();
  await using var input=await response.Content.ReadAsStreamAsync(token);using var memory=new MemoryStream();var buffer=new byte[4096];int read;
  while((read=await input.ReadAsync(buffer,token))>0){if(memory.Length+read>16384)throw new InvalidDataException("A configuração central retornou uma resposta inválida.");memory.Write(buffer,0,read);}
  var connection=JsonSerializer.Deserialize<SharedConnection>(memory.ToArray(),new JsonSerializerOptions(JsonSerializerDefaults.Web));
  if(connection is null||connection.InstanceId==Guid.Empty||connection.Version==Guid.Empty)throw new InvalidDataException("Não foi possível identificar a configuração central do Orbe.");
  return connection with{ApiUrl=ConnectionAddress.Normalize(connection.ApiUrl,local)};
 }
}
