using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Orbe.Server;

namespace Orbe.Server.Checks;
internal static class ChecksMain
{
 static int checks;
 const string Password="SyntheticPassword123";
 static void Check(bool yes,string label){if(!yes)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
 static async Task<JsonElement> Send(HttpClient c,HttpMethod method,string path,object? body=null,int expected=200,string? key=null)
 {
  using var request=new HttpRequestMessage(method,path);if(body is not null)request.Content=JsonContent.Create(body);if(key is not null)request.Headers.Add("Idempotency-Key",key);
  using var response=await c.SendAsync(request);if((int)response.StatusCode!=expected)throw new Exception($"{method} {path.Split('?')[0]}: esperado {expected}, recebido {(int)response.StatusCode}.");
  if(response.StatusCode==HttpStatusCode.NoContent)return default;var text=await response.Content.ReadAsStringAsync();return string.IsNullOrWhiteSpace(text)?default:JsonDocument.Parse(text).RootElement.Clone();
 }
 static Task<JsonElement> Get(HttpClient c,string path)=>Send(c,HttpMethod.Get,path);
 static string Id(JsonElement row)=>row.GetProperty("id").GetString()!;
 static object Version(JsonElement row)=>new{version=row.GetProperty("version").GetString()};
 static DateTimeOffset At(int h,int m=0)=>new(2026,11,2,h,m,0,TimeSpan.FromHours(-3));
 public static async Task Main()
 {
  var connection=Environment.GetEnvironmentVariable("ORBE_TEST_DATABASE")??throw new Exception("Configure ORBE_TEST_DATABASE para o PostgreSQL isolado.");
  var cs=new NpgsqlConnectionStringBuilder(connection);
  if(cs.Host!="127.0.0.1"||cs.Port!=55439||cs.Database!="orbe_cloud_test")throw new Exception("Os testes exigem o banco local exclusivo orbe_cloud_test na porta 55439.");
  await using(var reset=new NpgsqlConnection(connection)){await reset.OpenAsync();await using var cmd=new NpgsqlCommand("DROP SCHEMA public CASCADE; CREATE SCHEMA public",reset);await cmd.ExecuteNonQueryAsync();}
  using var factory=new OrbeFactory(connection);
  var anonymous=factory.CreateClient();
  var closed=await Get(anonymous,"/api/v1/public/info");Check(!closed.GetProperty("publicBookingOpen").GetBoolean(),"Reservas públicas fechadas por padrão");
  async Task<HttpClient> Login(string name)
  {
   var client=factory.CreateClient(new(){HandleCookies=false});var result=await Send(client,HttpMethod.Post,"/api/v1/auth/token",new{login=name,password=Password});client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",result.GetProperty("accessToken").GetString());return client;
  }
  var owner=await Login("WillianBN");
  var person1=await Send(owner,HttpMethod.Post,"/api/v1/owner/employees",new{userName="staff1",email="staff1@example.test",fullName="Profissional Um",password=Password});
  var person2=await Send(owner,HttpMethod.Post,"/api/v1/owner/employees",new{userName="staff2",email="staff2@example.test",fullName="Profissional Dois",password=Password});
  var e1=Id(person1);var e2=Id(person2);var staff1=await Login("staff1");var staff2=await Login("staff2");
  await Send(staff1,HttpMethod.Post,"/api/v1/owner/employees",new{userName="intruder",email="x@example.test",fullName="X",password=Password},403);Check(true,"Funcionário não cria outras contas da equipe");
  var service=await Send(owner,HttpMethod.Post,"/api/v1/owner/services",new{name="Atendimento",durationMinutes=60,priceCents=10000,enabled=true});var serviceId=Id(service);
  async Task<JsonElement> SetEmployee(string id,JsonElement row,bool enabled=true)=>await Send(owner,HttpMethod.Put,$"/api/v1/owner/employees/{id}",new{version=row.GetProperty("version").GetString(),displayName=id==e1?"Profissional Um":"Profissional Dois",enabled,services=new[]{serviceId}});
  person1=await SetEmployee(e1,person1);person2=await SetEmployee(e2,person2);
  object[] week=[new{day=1,window=new{startMinute=540,endMinute=720}},new{day=1,window=new{startMinute=780,endMinute=1080}}];
  async Task<JsonElement> SetHours(HttpClient client,string id,object[] weekly)
  {var row=await Get(client,$"/api/v1/team/hours?employeeId={id}");return await Send(client,HttpMethod.Put,$"/api/v1/team/hours?employeeId={id}",new{version=row.GetProperty("version").GetString(),weekly,exceptions=Array.Empty<object>()});}
  await SetHours(staff1,e1,week);await SetHours(staff2,e2,week);Check(true,"Cada funcionário define sua jornada sem aprovação");
  await Send(staff1,HttpMethod.Get,$"/api/v1/team/hours?employeeId={e2}",expected:403);Check(true,"Funcionário não consulta jornada privada de outro");
  var ownHours=await Get(staff1,$"/api/v1/team/hours?employeeId={e1}");
  await Send(staff1,HttpMethod.Put,$"/api/v1/team/hours?employeeId={e2}",new{version=ownHours.GetProperty("version").GetString(),weekly=week,exceptions=Array.Empty<object>()},403);Check(true,"Funcionário não altera jornada de outro");
  async Task<HttpClient> Client(string name)
  {
   var address=name+"@example.test";
   await Send(anonymous,HttpMethod.Post,"/api/v1/auth/register",new{fullName=name,email=address,phone="11999990000",birthDate="2000-01-01",password=Password,role="Owner",employeeId=e1},202);
   await Send(anonymous,HttpMethod.Post,"/api/v1/auth/token",new{login=address,password=Password},403);
   var mail=factory.Mail.Items.Last(x=>x.To==address);var url=new Uri(mail.Body[(mail.Body.IndexOf("http",StringComparison.Ordinal))..]);
   await Send(anonymous,HttpMethod.Post,"/api/v1/auth/confirm"+url.Query,new{});
   var client=await Login(address);var me=await Get(client,"/api/v1/auth/me");Check(me.GetProperty("roles").EnumerateArray().Single().GetString()=="Client","Confirmação de e-mail e papel Client fixado no servidor: "+name);return client;
  }
  var client1=await Client("cliente1");var client2=await Client("cliente2");
  await Send(client1,HttpMethod.Get,"/api/v1/team/entries",expected:403);await Send(client1,HttpMethod.Get,"/api/v1/owner/employees",expected:403);Check(true,"Cliente não acessa equipe ou financeiro");
  object Reservation(string employee,DateTimeOffset start)=>new{employeeId=employee,serviceId,start};
  await Send(client1,HttpMethod.Post,"/api/v1/bookings",Reservation(e1,At(9)),409,"closed-request");Check(true,"Reservas fechadas também são recusadas pela API");
  var settings=await Get(owner,"/api/v1/owner/settings");
  await Send(owner,HttpMethod.Put,"/api/v1/owner/settings",new{version=settings.GetProperty("version").GetString(),name="Orbe QA",importCompleted=true,publicBookingOpen=true,slotMinutes=15,leadMinutes=120,horizonDays=60,cancellationHours=24,timeZoneId="America/Sao_Paulo"});
  var slots=await Get(anonymous,$"/api/v1/public/availability?employeeId={e1}&serviceId={serviceId}&from=2026-11-02");
  Check(slots.GetProperty("days")[0].GetProperty("slots").GetArrayLength()==26,"Disponibilidade real respeita jornada dividida e duração");
  async Task<(HttpClient Client,int Code,JsonElement Row)> TryBook(HttpClient c,string key)
  {using var request=new HttpRequestMessage(HttpMethod.Post,"/api/v1/bookings"){Content=JsonContent.Create(Reservation(e1,At(9)))};request.Headers.Add("Idempotency-Key",key);using var response=await c.SendAsync(request);var row=await response.Content.ReadFromJsonAsync<JsonElement>();return(c,(int)response.StatusCode,row);}
  var race=await Task.WhenAll(TryBook(client1,"race-one"),TryBook(client2,"race-two"));
  Check(race.Count(x=>x.Code==200)==1&&race.Count(x=>x.Code==409)==1,"Duas clientes simultâneas: exatamente uma reserva confirmada");
  var winner=race.Single(x=>x.Code==200);var booking=winner.Row;var loser=race.Single(x=>x.Code==409).Client;
  var key=winner.Client==client1?"race-one":"race-two";
  var replay=await Send(winner.Client,HttpMethod.Post,"/api/v1/bookings",Reservation(e1,At(9)),key:key);Check(Id(replay)==Id(booking),"Idempotência retorna a mesma reserva");
  await Send(winner.Client,HttpMethod.Post,"/api/v1/bookings",Reservation(e1,At(10)),409,key);Check(true,"Chave de confirmação não pode ser reutilizada com outro horário");
  await Send(loser,HttpMethod.Post,$"/api/v1/bookings/{Id(booking)}/cancel",Version(booking),404);Check(true,"Cliente não cancela reserva de outra pessoa");
  var money=await Send(staff1,HttpMethod.Post,"/api/v1/team/entries",new{employeeId=e1,appointmentId=Id(booking),description="Recebimento vinculado",kind="Receita",date="2026-11-02",cents=10000});
  var loose=await Send(staff1,HttpMethod.Post,"/api/v1/team/entries",new{employeeId=e1,description="Avulso",kind="Despesa",date="2026-11-02",cents=1000});
  var otherMoney=await Get(staff2,"/api/v1/team/entries");Check(otherMoney.GetArrayLength()==0,"Financeiro individual não revela lançamentos de outro funcionário");
  await Send(staff2,HttpMethod.Delete,$"/api/v1/team/entries/{Id(money)}",Version(money),403);Check(true,"Funcionário não exclui financeiro de outro");
  await Send(staff1,HttpMethod.Post,"/api/v1/team/blocks",new{employeeId=e1,title="Compromisso privado",kind="Compromisso",start=At(9,30),end=At(10,30)},409);Check(true,"Compromisso que conflita com reserva é recusado");
  await Send(staff1,HttpMethod.Put,$"/api/v1/team/hours?employeeId={e1}",new{version=ownHours.GetProperty("version").GetString(),weekly=Array.Empty<object>(),exceptions=Array.Empty<object>()},409);Check(true,"Mudança de jornada que perde reserva é recusada");
  await Send(staff1,HttpMethod.Delete,$"/api/v1/team/appointments/{Id(booking)}",Version(booking),204);
  Check((await Get(staff1,"/api/v1/team/entries")).EnumerateArray().Single().GetProperty("id").GetString()==Id(loose),"Exclusão transacional remove financeiro vinculado e mantém avulso");
  var later=await Send(client1,HttpMethod.Post,"/api/v1/bookings",Reservation(e1,At(10).AddDays(7)),key:"later-one");
  await Send(client2,HttpMethod.Post,"/api/v1/bookings",Reservation(e1,At(11).AddDays(7)),key:"later-two");
  await Send(client1,HttpMethod.Post,$"/api/v1/bookings/{Id(later)}/move",new{version=later.GetProperty("version").GetString(),start=At(11).AddDays(7)},409);
  var retained=(await Get(client1,"/api/v1/bookings/mine")).EnumerateArray().Single(x=>Id(x)==Id(later));Check(retained.GetProperty("start").GetDateTimeOffset()==At(10).AddDays(7),"Remarcação que falha preserva horário antigo");
  var beforeCancel=await Send(staff1,HttpMethod.Post,"/api/v1/team/entries",new{employeeId=e1,appointmentId=Id(later),description="Já recebido",kind="Receita",date="2026-11-02",cents=10000});
  factory.Clock.Now=At(10).AddDays(6).AddSeconds(1);await Send(client1,HttpMethod.Post,$"/api/v1/bookings/{Id(later)}/cancel",Version(later),409);Check(true,"Cancelamento recusa prazo menor que 24 horas");
  factory.Clock.Now=At(10).AddDays(6);await Send(client1,HttpMethod.Post,$"/api/v1/bookings/{Id(later)}/cancel",Version(later),204);
  Check((await Get(staff1,"/api/v1/team/entries")).EnumerateArray().Any(x=>Id(x)==Id(beforeCancel)),"Cancelamento no limite exato libera reserva e preserva valores registrados");
  factory.Clock.Now=At(6);
  await Send(staff2,HttpMethod.Post,"/api/v1/team/blocks",new{employeeId=e2,title="Folga privada",kind="Folga",start=At(0),end=At(0).AddDays(1),notes="private-reason"});
  var hidden=await Get(anonymous,$"/api/v1/public/availability?employeeId={e2}&serviceId={serviceId}&from=2026-11-02");Check(hidden.GetProperty("days")[0].GetProperty("slots").GetArrayLength()==0&&!hidden.ToString().Contains("private-reason"),"Folga integral remove vagas sem expor motivo privado");
  using(var scope=factory.Services.CreateScope())
  {
   var db=scope.ServiceProvider.GetRequiredService<OrbeDb>();db.Appointments.Add(new(){EmployeeId=e1,Title="Colisão direta de teste",Start=At(11).AddDays(7).ToUniversalTime(),End=At(12).AddDays(7).ToUniversalTime()});
   try{await db.SaveChangesAsync();throw new Exception("Banco aceitou sobreposição");}catch(DbUpdateException e)when(e.InnerException is PostgresException{SqlState:"23P01"}){Check(true,"PostgreSQL impede sobreposição mesmo fora da API");}
  }
  var browser=factory.CreateClient();var csrf=await Get(browser,"/api/v1/auth/csrf");browser.DefaultRequestHeaders.Add("X-CSRF-TOKEN",csrf.GetProperty("token").GetString());await Send(browser,HttpMethod.Post,"/api/v1/auth/login",new{login="staff1",password=Password});browser.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");await Send(browser,HttpMethod.Put,"/api/v1/auth/me",new{fullName="Forjado",phone="11999990000"},400);Check(true,"Sessão do navegador exige proteção CSRF para alterar dados");
  var employees=await Get(owner,"/api/v1/owner/employees");var latest=employees.EnumerateArray().Single(x=>Id(x)==e2);await SetEmployee(e2,latest,false);await Send(staff2,HttpMethod.Get,"/api/v1/auth/me",expected:401);Check(true,"Desativar funcionário invalida acesso já emitido");
  await Send(client1,HttpMethod.Post,"/api/v1/auth/password",new{currentPassword=Password,newPassword="AnotherSyntheticPassword123"});await Send(client1,HttpMethod.Get,"/api/v1/auth/me",expected:401);Check(true,"Troca de senha revoga sessão anterior");
  Console.WriteLine($"{checks} verificações HTTP/PostgreSQL passaram com dados exclusivamente sintéticos.");
 }
}
internal sealed class TestClock : TimeProvider {public DateTimeOffset Now=new(2026,11,2,6,0,0,TimeSpan.FromHours(-3));public override DateTimeOffset GetUtcNow()=>Now.ToUniversalTime();}
internal sealed class MailCapture : IEmailDelivery
{
 public ConcurrentQueue<(string To,string Subject,string Body)> Items {get;}=new();public bool Ready=>true;
 public Task Send(string to,string subject,string body){Items.Enqueue((to,subject,body));return Task.CompletedTask;}
}
internal sealed class OrbeFactory(string connection):WebApplicationFactory<global::Program>
{
 public TestClock Clock {get;}=new();public MailCapture Mail {get;}=new();
 protected override void ConfigureWebHost(IWebHostBuilder builder)
 {
  builder.UseEnvironment("Testing");
  builder.UseContentRoot(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../src/Orbe.Server")));
  builder.ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(new Dictionary<string,string?>{{"ConnectionStrings:Orbe",connection},{"Owner:Email","owner@example.test"},{"Owner:Password","SyntheticPassword123"},{"PublicUrl","http://localhost"},{"Limits:AuthPerMinute","1000"}}));
  builder.ConfigureServices(s=>{s.RemoveAll<TimeProvider>();s.AddSingleton<TimeProvider>(Clock);s.RemoveAll<IEmailDelivery>();s.AddSingleton<IEmailDelivery>(Mail);});
 }
}
