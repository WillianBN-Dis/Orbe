using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Agenda.Core;

public sealed record AndroidUpdate(string Version,int Build,string Url,string Sha256,string Notes)
{
 public void Validate()
 {
  if(!System.Version.TryParse(Version,out _)||Build<1||Notes is null||Notes.Length>2000||!Regex.IsMatch(Sha256??"","\\A[0-9a-fA-F]{64}\\z")||
   !Uri.TryCreate(Url,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.Host!="github.com"||uri.UserInfo.Length>0||uri.Query.Length>0||uri.Fragment.Length>0||
   !uri.AbsolutePath.StartsWith("/WillianBN-Dis/Orbe/releases/download/",StringComparison.Ordinal)||!uri.AbsolutePath.EndsWith(".apk",StringComparison.OrdinalIgnoreCase))
   throw new InvalidDataException("As informações da atualização são inválidas. Tente novamente mais tarde.");
 }
}

public sealed class AppUpdates:IDisposable
{
 public const string FeedUrl="https://raw.githubusercontent.com/WillianBN-Dis/Orbe/codex/orbe-cloud/updates/android.json";
 private const long MaximumApk=150L*1024*1024;
 private readonly HttpClient http;
 public AppUpdates(HttpMessageHandler? handler=null)
 {http=handler is null?new(new HttpClientHandler{AllowAutoRedirect=false}):new(handler);http.Timeout=TimeSpan.FromMinutes(15);http.DefaultRequestHeaders.UserAgent.ParseAdd("Orbe-Android-Updater/1.0");}
 private async Task<HttpResponseMessage> Open(string url,CancellationToken token)
 {
  for(int hop=0;hop<6;hop++)
  {
   var uri=new Uri(url);if(uri.Scheme!="https"||uri.UserInfo.Length>0||!(uri.Host=="github.com"||uri.Host=="raw.githubusercontent.com"||uri.Host.EndsWith(".githubusercontent.com",StringComparison.Ordinal)))throw new InvalidDataException("Endereço de atualização não autorizado.");
   var response=await http.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,token);
   if(response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
   {var next=response.Headers.Location;response.Dispose();if(next is null)throw new InvalidDataException("Download de atualização indisponível.");url=new Uri(uri,next).AbsoluteUri;continue;}
   if(!response.IsSuccessStatusCode){response.Dispose();throw new HttpRequestException("O GitHub não disponibilizou a atualização agora. Tente novamente em alguns minutos.");}
   return response;
  }
  throw new InvalidDataException("O download foi redirecionado muitas vezes.");
 }
 public async Task<AndroidUpdate> Check(CancellationToken token=default)
 {
  using var response=await Open(FeedUrl+"?check="+DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),token);
  if(response.Content.Headers.ContentLength>16384)throw new InvalidDataException("Resposta de atualização inválida.");
  await using var input=await response.Content.ReadAsStreamAsync(token);using var memory=new MemoryStream();var buffer=new byte[4096];int length;
  while((length=await input.ReadAsync(buffer,token))>0){if(memory.Length+length>16384)throw new InvalidDataException("Resposta de atualização inválida.");memory.Write(buffer,0,length);}
  var update=JsonSerializer.Deserialize<AndroidUpdate>(memory.ToArray(),new JsonSerializerOptions(JsonSerializerDefaults.Web))??throw new InvalidDataException("Resposta de atualização vazia.");update.Validate();return update;
 }
 public async Task<string> Download(AndroidUpdate update,string cacheDirectory,IProgress<double>? progress=null,CancellationToken token=default)
 {
  update.Validate();Directory.CreateDirectory(cacheDirectory);var output=Path.Combine(cacheDirectory,$"orbe-{update.Build}.apk");
  if(File.Exists(output))
  {await using var existing=File.OpenRead(output);if(Convert.ToHexString(await SHA256.HashDataAsync(existing,token)).Equals(update.Sha256,StringComparison.OrdinalIgnoreCase)){progress?.Report(1);return output;}}
  var partial=output+".partial-"+Guid.NewGuid().ToString("N");
  try
  {
   using var response=await Open(update.Url,token);var total=response.Content.Headers.ContentLength;if(total>MaximumApk)throw new InvalidDataException("O pacote ultrapassa o tamanho permitido.");
   await using var input=await response.Content.ReadAsStreamAsync(token);using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);long received=0,lastReport=0;
   await using(var file=new FileStream(partial,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,true))
   {var buffer=new byte[65536];int count;while((count=await input.ReadAsync(buffer,token))>0){received+=count;if(received>MaximumApk)throw new InvalidDataException("O pacote ultrapassa o tamanho permitido.");hash.AppendData(buffer,0,count);await file.WriteAsync(buffer.AsMemory(0,count),token);if(received-lastReport>=1024*1024){progress?.Report(total>0?Math.Min(.99,(double)received/total.Value):0);lastReport=received;}}}
   if(received==0||total is not null&&received!=total||!Convert.ToHexString(hash.GetHashAndReset()).Equals(update.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("O pacote está incompleto ou não passou na verificação de integridade. Baixe novamente.");
   File.Move(partial,output,true);progress?.Report(1);return output;
  }
  finally{if(File.Exists(partial))File.Delete(partial);}
 }
 public void Dispose()=>http.Dispose();
}
