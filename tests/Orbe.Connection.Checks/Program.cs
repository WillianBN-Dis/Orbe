using Agenda.Core;
using Orbe.Cloud.Core;
using System.Net;
using System.Text.Json;

int count=0;void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);count++;}
foreach(var bad in new[]{"http://orbe.discloud.dev","https://127.0.0.1","https://orbe.discloud.dev.attacker.test","https://orbe.discloud.dev/path","https://user:password@orbe.discloud.dev","https://orbe.discloud.dev:8443","file:///tmp/orbe"})
{try{ConnectionAddress.Normalize(bad);throw new Exception("Unsafe connection accepted");}catch(ArgumentException){count++;}}
Check(ConnectionAddress.Normalize("https://Orbe.discloud.dev/")=="https://orbe.discloud.dev","HTTPS central normalizado sem caminho/credenciais");
var connection=new SharedConnection("https://next.discloud.dev",Guid.NewGuid(),Guid.NewGuid());
var found=await CloudRouting.Discover(new Handler(request=>{Check(request.Headers.Authorization is null,"Descoberta não envia credenciais de sessão");Check(request.RequestUri!.AbsolutePath=="/api/v1/public/connection","Descoberta usa ponto inicial estável");return JsonSerializer.Serialize(connection);}));
Check(found==connection,"Aplicativo recebe o endereço central para o mesmo espaço Orbe");
try{await CloudRouting.Discover(new Handler(_=>JsonSerializer.Serialize(connection with{ApiUrl="http://127.0.0.1"})));throw new Exception("Unsafe discovered destination accepted");}catch(ArgumentException){Check(true,"Resposta não pode encaminhar login para endereço privado");}
try{await CloudRouting.Discover(new Handler(_=>JsonSerializer.Serialize(connection with{InstanceId=Guid.Empty})));throw new Exception("Missing instance accepted");}catch(InvalidDataException){Check(true,"Descoberta exige identidade da instalação");}
try{await CloudRouting.Discover(new Handler(_=>new string('x',17000)));throw new Exception("Large discovery accepted");}catch(InvalidDataException){Check(true,"Resposta central tem tamanho limitado");}
using var cancelled=new CancellationTokenSource();cancelled.Cancel();try{await CloudRouting.Discover(new Handler(_=>JsonSerializer.Serialize(connection)),token:cancelled.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){Check(true,"Descoberta respeita cancelamento");}
Console.WriteLine($"{count} verificações de conexão passaram.");
sealed class Handler(Func<HttpRequestMessage,string> respond):HttpMessageHandler
{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){token.ThrowIfCancellationRequested();return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(respond(request))});}}
