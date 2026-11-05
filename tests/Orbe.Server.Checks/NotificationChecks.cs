using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Orbe.Server.Checks;
internal static class NotificationChecks
{
 static async Task<JsonElement> Send(HttpClient c,HttpMethod method,string path,object? body=null,int expected=200)
 {using var req=new HttpRequestMessage(method,"/api/v1"+path);if(body is not null)req.Content=JsonContent.Create(body);using var response=await c.SendAsync(req);if((int)response.StatusCode!=expected)throw new Exception($"Notification {method} {path}: expected {expected}, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");var json=await response.Content.ReadAsStringAsync();return string.IsNullOrWhiteSpace(json)?default:JsonSerializer.Deserialize<JsonElement>(json);}
 static void Check(bool value,string text){if(!value)throw new Exception(text);Console.WriteLine("PASS notifications "+text);}
 public static async Task Verify(HttpClient owner,HttpClient anonymous,OrbeFactory factory)
 {
  var previous=factory.Clock.Now;
  try{
   factory.Clock.Now=new DateTimeOffset(2026,11,2,9,0,0,TimeSpan.Zero);
   async Task<HttpClient> Login(string login,string password){var token=await Send(anonymous,HttpMethod.Post,"/auth/token",new{login,password});var c=factory.CreateClient(new(){HandleCookies=false});c.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token.GetProperty("accessToken").GetString());return c;}
   async Task<(string,HttpClient)> Staff(string name){var e=await Send(owner,HttpMethod.Post,"/owner/employees",new{userName=name,email=name+"@example.test",fullName=name,password="SyntheticPassword123"});return(e.GetProperty("id").GetString()!,await Login(name,"SyntheticPassword123"));}
   var (employee,staff)=await Staff("notice.staff");var (_,other)=await Staff("notice.other");var client=await Login("cliente1@example.test","AnotherSyntheticPassword123");var profile=await Send(client,HttpMethod.Get,"/auth/me");var clientId=profile.GetProperty("id").GetString();
   var start=factory.Clock.Now.AddHours(26);
   object Input(DateTimeOffset at,Guid? version=null)=>new{employeeId=employee,title="Serviço sintético",start=at,end=at.AddMinutes(30),clientId,legacyClient=(string?)null,notes=(string?)null,expectedCents=1000,version};
   var booking=await Send(staff,HttpMethod.Post,"/team/appointments",Input(start));var id=booking.GetProperty("id").GetString()!;
   async Task<JsonElement[]> Items(HttpClient c){var list=await Send(c,HttpMethod.Get,"/notifications");return list.GetProperty("items").EnumerateArray().Where(x=>x.GetProperty("appointmentId").GetString()==id).ToArray();}
   var rows=await Items(staff);Check(rows.Length==1&&rows[0].GetProperty("kind").GetString()=="Booking","novo agendamento aparece imediatamente sem antecipar lembretes");
   Check((await Items(owner)).Length==1&&(await Items(client)).Length==1&&(await Items(other)).Length==0,"Owner geral, profissional próprio, cliente próprio; isolamento de outro funcionário");
   var notice=rows[0].GetProperty("id").GetString();await Send(other,HttpMethod.Put,$"/notifications/{notice}/read",new{},404);await Send(other,HttpMethod.Delete,$"/notifications/{notice}",new{},404);Check(true,"IDs alheios não podem ser lidos nem excluídos");
   await Send(staff,HttpMethod.Put,$"/notifications/{notice}/read",new{},204);await Send(staff,HttpMethod.Put,$"/notifications/{notice}/read",new{},204);Check((await Items(staff))[0].GetProperty("readAt").ValueKind==JsonValueKind.String&&(await Items(client))[0].GetProperty("readAt").ValueKind==JsonValueKind.Null,"leitura idempotente e pessoal, compartilhada entre sessões da mesma conta");
   await Send(staff,HttpMethod.Delete,$"/notifications/{notice}",new{},204);await Send(staff,HttpMethod.Delete,$"/notifications/{notice}",new{},204);Check((await Items(staff)).Length==0&&(await Items(owner)).Length==1,"exclusão pessoal não ressuscita no polling nem apaga aviso de outra conta");
   factory.Clock.Now=start.AddDays(-1).AddTicks(-1);Check((await Items(staff)).Length==0,"24h ainda não atingidas não geram lembrete");factory.Clock.Now=start.AddDays(-1);Check((await Items(staff)).Single().GetProperty("kind").GetString()=="Day","lembrete no limite exato de 24h");
   factory.Clock.Now=start.AddHours(-1).AddTicks(-1);Check((await Items(staff)).Length==1,"uma hora ainda não atingida não gera lembrete");factory.Clock.Now=start.AddHours(-1);
   await Task.WhenAll(Enumerable.Range(0,5).Select(_=>Items(client)));rows=await Items(client);Check(rows.Length==3&&rows.Select(x=>x.GetProperty("id").GetString()).Distinct().Count()==3,"uma hora exata e consultas concorrentes sem duplicação");
   var unread=(await Send(staff,HttpMethod.Get,"/notifications")).GetProperty("unreadCount").GetInt32();Check(unread==2,"contador de não lidas exclui aviso lido e apagado");
   var next=start.AddDays(2);booking=await Send(staff,HttpMethod.Put,"/team/appointments/"+id,Input(next,booking.GetProperty("version").GetGuid()));rows=await Items(staff);Check(rows.Length==1&&rows[0].GetProperty("kind").GetString()=="Booking"&&rows[0].GetProperty("occurrenceStart").GetDateTimeOffset()==next,"remarcação invalida lembretes do horário anterior");
   await Send(staff,HttpMethod.Post,$"/bookings/{id}/cancel",new{version=booking.GetProperty("version").GetGuid()},204);Check((await Items(staff)).Length==0&&(await Items(client)).Length==0,"cancelamento retira avisos ativos e impede novos lembretes");
   var shortStart=factory.Clock.Now.AddMinutes(30);booking=await Send(staff,HttpMethod.Post,"/team/appointments",Input(shortStart));id=booking.GetProperty("id").GetString()!;rows=await Items(staff);Check(rows.Length==1&&rows[0].GetProperty("kind").GetString()=="Booking","reserva tardia não fabrica lembretes retroativos de dia/hora");
   await Send(staff,HttpMethod.Delete,"/team/appointments/"+id,new{version=booking.GetProperty("version").GetGuid()},204);Check((await Items(staff)).Length==0,"excluir agendamento remove avisos vinculados");
   await Send(anonymous,HttpMethod.Get,"/notifications",expected:401);await Send(staff,HttpMethod.Get,"/notifications?limit=101",expected:400);Check(true,"autenticação e limite de página validados no servidor");
  }finally{factory.Clock.Now=previous;}
 }
}
