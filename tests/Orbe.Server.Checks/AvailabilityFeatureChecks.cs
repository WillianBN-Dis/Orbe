using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Orbe.Server.Checks;

internal static class AvailabilityFeatureChecks
{
 const string Password="SyntheticPassword123";
 static string Id(JsonElement item)=>item.GetProperty("id").GetString()!;
 static object Version(JsonElement item)=>new{version=item.GetProperty("version").GetGuid()};
 static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS availability "+message);}
 static async Task<JsonElement> Send(HttpClient client,HttpMethod method,string path,object? body=null,int expected=200)
 {
  using var request=new HttpRequestMessage(method,"/api/v1"+path);
  if(body is not null)request.Content=JsonContent.Create(body);
  using var response=await client.SendAsync(request);
  if((int)response.StatusCode!=expected)throw new Exception($"Availability {method} {path}: expected {expected}, got {(int)response.StatusCode}");
  var text=await response.Content.ReadAsStringAsync();
  return string.IsNullOrWhiteSpace(text)?default:JsonSerializer.Deserialize<JsonElement>(text);
 }
 public static async Task Verify(HttpClient owner,HttpClient anonymous,OrbeFactory factory)
 {
  var previous=factory.Clock.Now;var ready=factory.Mail.Ready;factory.Mail.Ready=true;
  factory.Clock.Now=new DateTimeOffset(2026,11,2,6,0,0,TimeSpan.Zero);
  try{
   async Task<(string,HttpClient)> Staff(string name){
    var e=await Send(owner,HttpMethod.Post,"/owner/employees",new{userName=name,email=name+"@example.test",fullName=name,password=Password});
    var session=await Send(anonymous,HttpMethod.Post,"/auth/token",new{login=name,password=Password});
    var c=factory.CreateClient(new(){HandleCookies=false});
    c.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",session.GetProperty("accessToken").GetString());
    return(Id(e),c);
   }
   var (employee,staff)=await Staff("dates.staff");using var staffLifetime=staff;
   var (_,other)=await Staff("dates.other");using var otherLifetime=other;
   var manual=await Send(staff,HttpMethod.Post,"/team/appointments",new{employeeId=employee,title="Manual fora da jornada",start="2026-11-04T23:00:00Z",end="2026-11-04T23:30:00Z",expectedCents=0});
   var hours=await Send(staff,HttpMethod.Get,"/team/hours");
   var weekly=new[]{new{day=1,window=new{startMinute=540,endMinute=720}}};
   await Send(staff,HttpMethod.Put,"/team/hours",new{version=hours.GetProperty("version").GetGuid(),weekly,exceptions=Array.Empty<object>()});
   hours=await Send(staff,HttpMethod.Get,"/team/hours");
   var exceptions=new[]{
    new{date="2026-11-03",windows=new[]{new{startMinute=540,endMinute=1080}}},
    new{date="2026-11-05",windows=new[]{new{startMinute=540,endMinute=1080}}}
   };
   await Send(staff,HttpMethod.Put,"/team/hours",new{version=hours.GetProperty("version").GetGuid(),weekly,exceptions});
   hours=await Send(staff,HttpMethod.Get,"/team/hours");
   Check(hours.GetProperty("exceptions").GetArrayLength()==2,"datas especiais persistem com atendimento manual externo em outra data");
   await Send(other,HttpMethod.Get,"/team/hours?employeeId="+employee,expected:403);
   var service=await Send(staff,HttpMethod.Post,"/team/services",new{name="Disponibilidade QA",durationMinutes=30,priceCents=1000,enabled=true});
   var settings=await Send(owner,HttpMethod.Get,"/owner/settings");
   await Send(owner,HttpMethod.Put,"/owner/settings",new{version=settings.GetProperty("version").GetGuid(),name="Orbe",importCompleted=true,publicBookingOpen=true,slotMinutes=15,leadMinutes=0,horizonDays=60,cancellationHours=24,timeZoneId="America/Sao_Paulo"});
   var available=await Send(anonymous,HttpMethod.Get,"/public/availability?employeeId="+employee+"&serviceId="+Id(service)+"&from=2026-11-03&days=1");
   var slots=available.GetProperty("days")[0].GetProperty("slots").EnumerateArray().ToArray();
   Check(slots.Any(s=>s.GetProperty("start").GetDateTimeOffset().ToUniversalTime()==DateTimeOffset.Parse("2026-11-03T12:00:00Z"))&&slots.Any(s=>s.GetProperty("end").GetDateTimeOffset().ToUniversalTime()==DateTimeOffset.Parse("2026-11-03T21:00:00Z")),"site oferece todo o período especial em um dia sem jornada semanal");
   var occupied=await Send(staff,HttpMethod.Post,"/team/appointments",new{employeeId=employee,title="Dentro do período especial",start="2026-11-03T13:00:00Z",end="2026-11-03T13:30:00Z",expectedCents=0});
   hours=await Send(staff,HttpMethod.Get,"/team/hours");
   var oldVersion=hours.GetProperty("version").GetGuid();
   await Send(staff,HttpMethod.Put,"/team/hours",new{version=oldVersion,weekly,exceptions=new[]{new{date="2026-11-03",windows=new[]{new{startMinute=720,endMinute=1080}}}}},409);
   Check((await Send(staff,HttpMethod.Get,"/team/hours")).GetProperty("version").GetGuid()==oldVersion,"conflito reverte toda a disponibilidade");
   await Send(staff,HttpMethod.Put,"/team/hours",new{version=oldVersion,weekly,exceptions},200);
   Check((await Send(staff,HttpMethod.Get,"/team/hours")).GetProperty("exceptions").GetArrayLength()==2,"salvar novamente a mesma data não duplica exceções");
   var failedBatch=Guid.NewGuid();
   await Send(staff,HttpMethod.Post,"/team/blocks/batch",new{batchId=failedBatch,employeeId=employee,title="Lote com conflito",kind="Folga",dates=new[]{"2026-11-03","2026-11-05"},allDay=false,startTime="10:00:00",endTime="11:00:00"},409);
   Check(!(await Send(staff,HttpMethod.Get,"/team/appointments")).EnumerateArray().Any(a=>a.GetProperty("title").GetString()=="Lote com conflito"),"nenhuma data é gravada se uma data do lote tiver conflito");
   var batchId=Guid.NewGuid();
   var input=new{batchId,employeeId=employee,title="Lote sintético",kind="Compromisso",dates=new[]{"2026-11-06","2026-11-07"},allDay=false,startTime="09:00:00",endTime="10:00:00"};
   await Task.WhenAll(Send(staff,HttpMethod.Post,"/team/blocks/batch",input),Send(staff,HttpMethod.Post,"/team/blocks/batch",input));
   var items=(await Send(staff,HttpMethod.Get,"/team/appointments")).EnumerateArray().Where(a=>a.GetProperty("title").GetString()=="Lote sintético").ToArray();
   Check(items.Length==2,"repetir confirmação concorrente não duplica bloqueios");
   await Send(other,HttpMethod.Post,"/team/blocks/batch",input,403);
   await Send(staff,HttpMethod.Delete,"/team/appointments/"+Id(items[0]),Version(items[0]),204);
   await Send(staff,HttpMethod.Post,"/team/blocks/batch",input,409);
   Check((await Send(staff,HttpMethod.Get,"/team/appointments")).EnumerateArray().Count(a=>a.GetProperty("title").GetString()=="Lote sintético")==1,"repetição não recria bloqueio removido individualmente");
   var ownerId=Id(await Send(owner,HttpMethod.Get,"/auth/me"));
   var baseline=(await Send(owner,HttpMethod.Get,"/notifications")).GetProperty("unreadCount").GetInt32();
   using(var scope=factory.Services.CreateScope()){
    var db=scope.ServiceProvider.GetRequiredService<OrbeDb>();
    db.Notifications.Add(new(){UserId=ownerId,AppointmentId=Id(manual),Kind="Booking",OccurrenceStart=manual.GetProperty("start").GetDateTimeOffset(),DeliverAt=factory.Clock.Now});
    await db.SaveChangesAsync();
   }
   var inbox=await Send(owner,HttpMethod.Get,"/notifications");
   Check(inbox.GetProperty("unreadCount").GetInt32()==baseline&&!inbox.GetProperty("items").EnumerateArray().Any(n=>n.GetProperty("appointmentId").GetString()==Id(manual)),"aviso administrativo antigo fica oculto e sai do contador");
  }finally{factory.Clock.Now=previous;factory.Mail.Ready=ready;}
 }
}
