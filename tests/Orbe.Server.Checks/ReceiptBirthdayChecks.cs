using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Orbe.Server;

namespace Orbe.Server.Checks;
internal static class ReceiptBirthdayChecks
{
 const string Password="SyntheticPassword123";
 static string Id(JsonElement x)=>x.GetProperty("id").GetString()!;
 static void Check(bool condition,string label){if(!condition)throw new Exception(label);Console.WriteLine("PASS receipts/birthdays "+label);}
 static async Task<JsonElement> Send(HttpClient client,HttpMethod method,string path,object? body=null,int expected=200)
 {
  using var request=new HttpRequestMessage(method,"/api/v1"+path);if(body!=null)request.Content=JsonContent.Create(body);
  using var response=await client.SendAsync(request);var text=await response.Content.ReadAsStringAsync();
  if((int)response.StatusCode!=expected)throw new Exception($"Receipt test {method} {path}: {(int)response.StatusCode}, expected {expected}: {text}");
  return string.IsNullOrWhiteSpace(text)?default:JsonSerializer.Deserialize<JsonElement>(text);
 }
 public static async Task Verify(HttpClient owner,HttpClient anonymous,OrbeFactory factory)
 {
  var old=factory.Clock.Now;factory.Clock.Now=new(2026,11,2,15,0,0,TimeSpan.Zero);
  try{
   async Task<(string,HttpClient,string)> Staff(string name){
    var row=await Send(owner,HttpMethod.Post,"/owner/employees",new{userName=name,email=name+"@example.test",fullName=name,password=Password});
    var session=await Send(anonymous,HttpMethod.Post,"/auth/token",new{login=name,password=Password});
    var client=factory.CreateClient(new(){HandleCookies=false});client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",session.GetProperty("accessToken").GetString());
    return (Id(row),client,session.GetProperty("profile").GetProperty("id").GetString()!);
   }
   var (employee,staff,userId)=await Staff("receipt.staff");var(otherEmployee,other,_)=await Staff("receipt.other");
   string customer;
   using(var scope=factory.Services.CreateScope()){
    var db=scope.ServiceProvider.GetRequiredService<OrbeDb>();var settings=await db.Settings.SingleAsync();settings.AutoFinanceStart=factory.Clock.Now.AddDays(-1);
    var users=scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var user=new AppUser{UserName="birthday.client",Email="birthday.client@example.test",EmailConfirmed=true,FullName="Cliente aniversário sintética",BirthDate=new(2000,2,29),PhoneNumber="11999990000"};
    if(!(await users.CreateAsync(user,Password)).Succeeded)throw new Exception("Synthetic client creation failed");
    await users.AddToRoleAsync(user,Roles.Client);customer=user.Id;await db.SaveChangesAsync();
   }
   var sequence=0;
   async Task<JsonElement> Appointment(string title,bool flexible=false,int day=2,string? client=null){var start=new DateTimeOffset(2026,11,day,6,0,0,TimeSpan.Zero).AddMinutes(30*sequence++);return await Send(staff,HttpMethod.Post,"/team/appointments",new{
    employeeId=employee,title,start,end=start.AddMinutes(30),expectedCents=7000,priceFrom=flexible,clientId=client});}
   var manual=await Appointment("Recebimento manual",client:customer);
   object Payment(string appointment,string? key=null)=>new{id=key??Guid.NewGuid().ToString("N"),employeeId=employee,appointmentId=appointment,description="Pagamento QA",kind="Receita",date="2026-11-02",cents=7000};
   var request=Payment(Id(manual));var paid=await Send(staff,HttpMethod.Post,"/team/entries",request);
   await Send(staff,HttpMethod.Post,"/team/entries",request);
   await Send(staff,HttpMethod.Post,"/team/entries",Payment(Id(manual)),409);
   await Send(other,HttpMethod.Post,"/team/entries",Payment(Id(manual)),403);
   Check((await Send(staff,HttpMethod.Get,"/team/entries")).GetArrayLength()==1,"novo identificador não duplica recebimento; repetição exata é segura e ACL preservada");
   var fixedItem=await Appointment("Automático");var flexible=await Appointment("Preço flexível",true);
   var flexibleEdit=await Send(staff,HttpMethod.Put,"/team/appointments/"+Id(flexible),new{employeeId=employee,title=flexible.GetProperty("title").GetString(),start=flexible.GetProperty("start").GetDateTimeOffset(),end=flexible.GetProperty("end").GetDateTimeOffset(),expectedCents=7000,version=flexible.GetProperty("version").GetGuid()});
   Check(flexibleEdit.GetProperty("priceFrom").GetBoolean(),"edição por aplicativo antigo preserva preço flexível");
   var cancelled=await Appointment("Cancelado");var pending=await Appointment("Pendente");var future=await Appointment("Futuro",day:3);var historical=await Appointment("Histórico",day:1);
   using(var scope=factory.Services.CreateScope()){
    var db=scope.ServiceProvider.GetRequiredService<OrbeDb>();
    (await db.Appointments.SingleAsync(a=>a.Id==Id(cancelled))).Status="Cancelled";
    (await db.Appointments.SingleAsync(a=>a.Id==Id(pending))).Status="Pending";
    await db.SaveChangesAsync();
   }
   async Task Tick(){using var scope=factory.Services.CreateScope();await scope.ServiceProvider.GetRequiredService<AutomaticReceipts>().Run();}
   await Task.WhenAll(Tick(),Tick());await Tick();
   var entries=(await Send(staff,HttpMethod.Get,"/team/entries")).EnumerateArray().ToArray();
   Check(entries.Length==2&&entries.Count(e=>e.GetProperty("appointmentId").GetString()==Id(fixedItem))==1,"rotina concorrente registra um valor fixo e não duplica o manual");
   var auto=entries.Single(e=>e.GetProperty("appointmentId").GetString()==Id(fixedItem));
   Check(auto.GetProperty("automatic").GetBoolean()&&auto.GetProperty("date").GetString()=="2026-11-02"&&auto.GetProperty("cents").GetInt64()==7000,"valor congelado, origem automática e data no fuso da empresa");
   Check(!entries.Any(e=>new[]{Id(cancelled),Id(pending),Id(future),Id(flexible),Id(historical)}.Contains(e.GetProperty("appointmentId").GetString())),"não lança cancelados, pendentes, futuros, flexíveis ou histórico anterior à ativação");
   var edit=new{version=auto.GetProperty("version").GetGuid(),description="Valor corrigido",date="2026-11-02",cents=8000};
   var edited=await Send(staff,HttpMethod.Put,"/team/entries/"+Id(auto),edit);await Send(staff,HttpMethod.Put,"/team/entries/"+Id(auto),edit,409);
   await Send(other,HttpMethod.Put,"/team/entries/"+Id(auto),new{version=edited.GetProperty("version").GetGuid(),description="Inválido",date="2026-11-02",cents=9000},403);
   Check(edited.GetProperty("cents").GetInt64()==8000,"edição altera o existente e recusa versão antiga ou outro profissional");
   var notices=await Send(staff,HttpMethod.Get,"/notifications");
   var payment=notices.GetProperty("items").EnumerateArray().Single(n=>n.GetProperty("kind").GetString()=="Payment");
   Check(payment.GetProperty("appointmentId").GetString()==Id(flexible),"preço flexível gera aviso de informar valor, não receita");
   await Send(staff,HttpMethod.Get,"/notifications");
   Check(!(await Send(other,HttpMethod.Get,"/notifications")).GetProperty("items").EnumerateArray().Any(n=>n.GetProperty("kind").GetString()=="Payment"),"aviso financeiro pertence só ao responsável");
   await Send(staff,HttpMethod.Post,"/team/entries",Payment(Id(flexible)));
   Check(!(await Send(staff,HttpMethod.Get,"/notifications")).GetProperty("items").EnumerateArray().Any(n=>n.GetProperty("kind").GetString()=="Payment"),"registrar valor retira aviso e contador pendentes");
   await Send(staff,HttpMethod.Delete,"/team/entries/"+Id(edited),new{version=edited.GetProperty("version").GetGuid()},204);await Tick();
   Check(!(await Send(staff,HttpMethod.Get,"/team/entries")).EnumerateArray().Any(e=>e.GetProperty("appointmentId").GetString()==Id(fixedItem)),"recebimento excluído não reaparece automaticamente");
   var race=await Appointment("Concorrência manual e automática");
   var posting=staff.PostAsJsonAsync("/api/v1/team/entries",Payment(Id(race)));var automaticTick=Tick();
   using(var response=await posting)Check(response.IsSuccessStatusCode||(int)response.StatusCode==409,"concorrência manual/automático aceita um vencedor");
   await automaticTick;await Tick();Check((await Send(staff,HttpMethod.Get,"/team/entries")).EnumerateArray().Count(e=>e.GetProperty("appointmentId").GetString()==Id(race))==1,"automático após manual preserva lançamento único");
   factory.Clock.Now=factory.Clock.Now.AddDays(2);await Tick();
   Check((await Send(staff,HttpMethod.Get,"/team/entries")).EnumerateArray().Count(e=>e.GetProperty("appointmentId").GetString()==Id(future))==1,"retomada após interrupção processa atendimento que terminou com a rotina parada");
   var boundary=await Appointment("Data local de término");
   using(var scope=factory.Services.CreateScope()){
    var db=scope.ServiceProvider.GetRequiredService<OrbeDb>();var item=await db.Appointments.SingleAsync(a=>a.Id==Id(boundary));item.Start=new DateTimeOffset(2026,11,2,0,0,0,TimeSpan.Zero);item.End=item.Start.AddHours(1);await db.SaveChangesAsync();
   }
   await Tick();
   Check((await Send(staff,HttpMethod.Get,"/team/entries")).EnumerateArray().Single(e=>e.GetProperty("appointmentId").GetString()==Id(boundary)).GetProperty("date").GetString()=="2026-11-01","término após meia-noite UTC usa data local da empresa");
   var birthday=await Send(staff,HttpMethod.Get,"/team/birthdays?year=2027");
   Check(birthday.GetArrayLength()==1&&birthday[0].GetProperty("date").GetString()=="2027-02-28"&&!birthday[0].TryGetProperty("birthDate",out _),"usa aniversário da conta sem duplicar nem revelar ano de nascimento");
   Check((await Send(staff,HttpMethod.Get,"/team/birthdays?year=2028"))[0].GetProperty("date").GetString()=="2028-02-29","29 de fevereiro preservado em ano bissexto");
   Check((await Send(other,HttpMethod.Get,"/team/birthdays?year=2027")).GetArrayLength()==0,"outro profissional não vê cliente sem vínculo");
   await Send(other,HttpMethod.Get,"/team/birthdays?year=2027&employeeId="+employee,expected:403);
   using(var scope=factory.Services.CreateScope()){
    var db=scope.ServiceProvider.GetRequiredService<OrbeDb>();var user=await db.Users.SingleAsync(u=>u.Id==customer);user.BirthDate=new(2000,5,12);
    db.BusinessId=999;db.Settings.Add(new(){Id=999,Name="Empresa aniversário QA",Slug="birthday-qa"});db.Employees.Add(new(){Id="birthday-company-admin",UserId=(await db.Users.SingleAsync(u=>u.UserName=="WillianBN")).Id,DisplayName="Admin QA",Enabled=true,Administrator=true});await db.SaveChangesAsync();
   }
   await Send(staff,HttpMethod.Delete,"/team/appointments/"+Id(manual),new{version=manual.GetProperty("version").GetGuid()},204);
   Check((await Send(staff,HttpMethod.Get,"/team/birthdays?year=2027"))[0].GetProperty("date").GetString()=="2027-05-12","alteração na conta reflete nas empresas vinculadas");
   var otherBusiness=factory.CreateClient(new(){HandleCookies=false});otherBusiness.DefaultRequestHeaders.Authorization=owner.DefaultRequestHeaders.Authorization;otherBusiness.DefaultRequestHeaders.Add("X-Orbe-Business","birthday-qa");
   Check((await Send(otherBusiness,HttpMethod.Get,"/team/birthdays?year=2027")).GetArrayLength()==0,"empresa sem agendamento não herda aniversários");
  }finally{factory.Clock.Now=old;}
 }
}
