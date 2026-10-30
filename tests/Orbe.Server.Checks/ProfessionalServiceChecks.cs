using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Orbe.Server;

namespace Orbe.Server.Checks;
internal static class ProfessionalServiceChecks
{
 private const string Password="SyntheticPassword123";
 private static async Task<JsonElement> Send(HttpClient c,HttpMethod method,string path,object? input=null,int expected=200)
 {using var request=new HttpRequestMessage(method,"/api/v1"+path);if(input is not null)request.Content=JsonContent.Create(input);if(path=="/bookings")request.Headers.Add("Idempotency-Key",Guid.NewGuid().ToString());using var response=await c.SendAsync(request);if((int)response.StatusCode!=expected)throw new Exception($"Service regression {method} {path.Split('?')[0]} expected {expected}, got {(int)response.StatusCode}");return response.StatusCode==System.Net.HttpStatusCode.NoContent?default:await response.Content.ReadFromJsonAsync<JsonElement>();}
 private static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS 2.0.1 "+message);}
 private static string Id(JsonElement row)=>row.GetProperty("id").GetString()!;
 private static object Input(JsonElement row,string name,long cents=3500)=>new{name,durationMinutes=30,priceCents=cents,enabled=true,version=row.GetProperty("version").GetString()};
 public static async Task Verify(HttpClient owner,HttpClient anonymous,OrbeFactory factory)
 {
  async Task<(string Employee,HttpClient Client)> Staff(string name){var item=await Send(owner,HttpMethod.Post,"/owner/employees",new{userName=name,email=name+"@example.test",fullName="Profissional "+name,password=Password});var session=await Send(anonymous,HttpMethod.Post,"/auth/token",new{login=name,password=Password});var client=factory.CreateClient(new(){HandleCookies=false});client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",session.GetProperty("accessToken").GetString());return(Id(item),client);}
  var (a,staffA)=await Staff("services.a");var (b,staffB)=await Staff("services.b");
  var created=await Send(staffA,HttpMethod.Post,"/team/services?employeeId="+a,new{name="Serviço A",durationMinutes=30,priceCents=2500,enabled=true});var id=Id(created);
  var path="/team/services/"+id+"?employeeId="+a;
  var mine=await Send(staffA,HttpMethod.Get,"/team/services");var item=mine.EnumerateArray().Single(x=>Id(x)==id);
  Check(item.GetProperty("employeeId").GetString()==a,"profissional cria e consulta seu próprio serviço");
  await Send(staffB,HttpMethod.Get,"/team/services?employeeId="+a,expected:403);
  await Send(staffB,HttpMethod.Put,path,Input(item,"Tentativa indevida"),403);
  await Send(staffB,HttpMethod.Delete,path,new{version=item.GetProperty("version").GetString()},403);
  await Send(staffB,HttpMethod.Post,"/team/services?employeeId="+a,new{name="Tentativa",durationMinutes=30,priceCents=1,enabled=true},403);
  Check(true,"outro profissional não consulta nem cria/edita/exclui serviços alheios");
  await Send(staffA,HttpMethod.Put,path,Input(item,"Serviço atualizado"));
  await Send(staffA,HttpMethod.Put,path,Input(item,"Versão antiga"),409);
  var publicA=await Send(anonymous,HttpMethod.Get,"/public/services?employeeId="+a);var publicB=await Send(anonymous,HttpMethod.Get,"/public/services?employeeId="+b);
  Check(publicA.EnumerateArray().Single().GetProperty("name").GetString()=="Serviço atualizado"&&!publicB.EnumerateArray().Any(),"catálogo público contém somente os serviços do profissional selecionado");
  item=(await Send(owner,HttpMethod.Get,"/team/services?employeeId="+a)).EnumerateArray().Single();
  await Send(owner,HttpMethod.Put,path,Input(item,"Ajustado pelo administrador",4000));
  var ownerCreated=await Send(owner,HttpMethod.Post,"/team/services?employeeId="+b,new{name="Serviço B",durationMinutes=45,priceCents=4500,enabled=true});
  Check((await Send(owner,HttpMethod.Get,"/team/services")).EnumerateArray().Any(x=>Id(x)==Id(ownerCreated)),"administrador cria e edita serviços de qualquer profissional e consulta todos");
  // Existing shared catalog entries remain independent when edited per professional.
  var legacy=await Send(owner,HttpMethod.Post,"/owner/services",new{name="Legado compartilhado",durationMinutes=60,priceCents=1000,enabled=true});
  async Task Assign(string employee,string service){var row=(await Send(owner,HttpMethod.Get,"/owner/employees")).EnumerateArray().Single(x=>Id(x)==employee);var assigned=row.GetProperty("services").EnumerateArray().Select(x=>x.GetString()!).Append(service).ToArray();await Send(owner,HttpMethod.Put,"/owner/employees/"+employee,new{version=row.GetProperty("version").GetString(),displayName=row.GetProperty("displayName").GetString(),enabled=true,services=assigned});}
  await Assign(a,Id(legacy));await Assign(b,Id(legacy));
  var shared=(await Send(staffA,HttpMethod.Get,"/team/services")).EnumerateArray().Single(x=>Id(x)==Id(legacy));
  await Send(staffA,HttpMethod.Put,"/team/services/"+Id(legacy)+"?employeeId="+a,Input(shared,"Meu legado",7000));
  var other=(await Send(anonymous,HttpMethod.Get,"/public/services?employeeId="+b)).EnumerateArray().Single(x=>Id(x)==Id(legacy));Check(other.GetProperty("name").GetString()=="Legado compartilhado"&&other.GetProperty("priceCents").GetInt64()==1000,"edição de oferta legada não modifica nome/preço de outro profissional");
  var hours=await Send(staffA,HttpMethod.Get,"/team/hours?employeeId="+a);
  await Send(staffA,HttpMethod.Put,"/team/hours?employeeId="+a,new{version=hours.GetProperty("version").GetString(),weekly=new[]{new{day=1,window=new{startMinute=540,endMinute=720}},new{day=1,window=new{startMinute=780,endMinute=1080}}},exceptions=Array.Empty<object>()});
  factory.Clock.Now=new DateTimeOffset(2026,11,2,6,0,0,TimeSpan.FromHours(-3));
  using(var scope=factory.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<OrbeDb>();(await db.Settings.SingleAsync()).PublicBookingOpen=true;await db.SaveChangesAsync();}
  var slots=await Send(anonymous,HttpMethod.Get,$"/public/availability?employeeId={a}&serviceId={id}&from=2026-11-02");Check(slots.GetProperty("days")[0].GetProperty("slots").GetArrayLength()>0,"disponibilidade usa a duração da oferta individual");
  var session=await Send(anonymous,HttpMethod.Post,"/auth/token",new{login="cliente1@example.test",password="AnotherSyntheticPassword123"});using var client=factory.CreateClient(new(){HandleCookies=false});client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",session.GetProperty("accessToken").GetString());
  var booking=await Send(client,HttpMethod.Post,"/bookings",new{employeeId=a,serviceId=id,start="2026-11-02T10:30:00-03:00"});
  Check(booking.GetProperty("expectedCents").GetInt64()==4000&&booking.GetProperty("durationMinutes").GetInt32()==30,"reserva congela preço e duração personalizados");
  item=(await Send(staffA,HttpMethod.Get,"/team/services")).EnumerateArray().Single(x=>Id(x)==id);await Send(staffA,HttpMethod.Delete,path,new{version=item.GetProperty("version").GetString()},204);
  var historic=await Send(client,HttpMethod.Get,"/bookings/mine");Check(historic.EnumerateArray().Any(x=>Id(x)==Id(booking)&&x.GetProperty("expectedCents").GetInt64()==4000),"exclusão de serviço preserva agendamentos e valores anteriores");
  Check(!(await Send(anonymous,HttpMethod.Get,"/public/services?employeeId="+a)).EnumerateArray().Any(x=>Id(x)==id),"serviço excluído deixa o catálogo público");
  var target=(await Send(owner,HttpMethod.Get,"/team/services?employeeId="+b)).EnumerateArray().Single(x=>Id(x)==Id(ownerCreated));await Send(owner,HttpMethod.Delete,"/team/services/"+Id(target)+"?employeeId="+b,new{version=target.GetProperty("version").GetString()},204);Check(true,"administrador exclui serviço de outro profissional");
 }
}
