using Agenda.Core;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var bytes=Encoding.UTF8.GetBytes("synthetic-apk-for-download-integrity-tests");var hash=Convert.ToHexString(SHA256.HashData(bytes));
var update=new AndroidUpdate("2.0.0",20001,"https://github.com/WillianBN-Dis/Orbe/releases/download/v2.0.0-ajuste.1/Orbe-2.0.0-ajuste.1.apk",hash,"Ajuste de atualização");
int checks=0;void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);checks++;}
void Reject(AndroidUpdate item){try{item.Validate();throw new Exception("Metadata accepted");}catch(InvalidDataException){}}
foreach(var bad in new[]{update with{Url="http://github.com/WillianBN-Dis/Orbe/releases/download/v2/a.apk"},update with{Url="https://github.com/another/repo/releases/download/v2/a.apk"},update with{Url="https://example.com/update.apk"},update with{Sha256="broken"},update with{Build=0}}){Reject(bad);checks++;}Console.WriteLine("PASS invalid origin, protocol, digest and build rejected");
var directory=Path.Combine(Path.GetTempPath(),"orbe-update-check-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
try
{
 using var service=new AppUpdates(new Handler((request)=>request.RequestUri!.Host=="raw.githubusercontent.com"?new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(update))}:new(HttpStatusCode.OK){Content=new ByteArrayContent(bytes)}));
 var found=await service.Check();Check(found.Build==20001&&found.Version=="2.0.0","same visible version can advertise a newer build");
 var file=await service.Download(found,directory);Check(File.ReadAllBytes(file).SequenceEqual(bytes),"download verified and moved atomically");
 using var cached=new AppUpdates(new Handler(_=>throw new Exception("Cache should avoid network")));Check(await cached.Download(found,directory)==file,"verified cache reused when installation permission requires returning");
 using var corrupt=new AppUpdates(new Handler(_=>new(HttpStatusCode.OK){Content=new ByteArrayContent([1,2,3])}));
 try{await corrupt.Download(found with{Build=20002},directory);throw new Exception("Corrupt APK accepted");}catch(InvalidDataException){Check(!Directory.GetFiles(directory,"*.partial-*").Any()&&!File.Exists(Path.Combine(directory,"orbe-20002.apk")),"corrupt download discarded without installer file");}
 using var redirected=new AppUpdates(new Handler(_=>new(HttpStatusCode.Redirect){Headers={Location=new Uri("http://evil.example/update.apk")}}));
 try{await redirected.Download(found with{Build=20003},directory);throw new Exception("Redirect accepted");}catch(InvalidDataException){Check(true,"redirect cannot downgrade HTTPS or leave trusted hosts");}
 using var cancelled=new CancellationTokenSource();cancelled.Cancel();try{await service.Download(found with{Build=20004},directory,token:cancelled.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){Check(!Directory.GetFiles(directory,"*.partial-*").Any(),"cancelled download leaves no partial package");}
 Console.WriteLine($"{checks} update checks passed using synthetic data.");
}finally{Directory.Delete(directory,true);}
sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> handler):HttpMessageHandler
{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){token.ThrowIfCancellationRequested();return Task.FromResult(handler(request));}}
