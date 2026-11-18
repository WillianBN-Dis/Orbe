using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace Orbe.Server.Checks;

internal static class BusinessFeatureChecks
{
 const string Password="SyntheticPassword123";
 static void Check(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS business features "+label);}
 static string Id(JsonElement item)=>item.GetProperty("id").GetString()!;
 static async Task<JsonElement> Send(HttpClient c,HttpMethod method,string path,object? body=null,int expected=200)
 {
  using var request=new HttpRequestMessage(method,"/api/v1"+path);
  if(body is not null)request.Content=JsonContent.Create(body);
  using var response=await c.SendAsync(request);
  if((int)response.StatusCode!=expected)throw new Exception($"Feature {method} {path}: expected {expected}, got {(int)response.StatusCode}");
  var content=await response.Content.ReadAsStringAsync();
  return string.IsNullOrWhiteSpace(content)?default:JsonSerializer.Deserialize<JsonElement>(content);
 }
 public static async Task Verify(HttpClient owner,HttpClient anonymous,OrbeFactory factory)
 {
  var previous=factory.Clock.Now;var mailReady=factory.Mail.Ready;factory.Mail.Ready=true;
  try{
   factory.Clock.Now=new DateTimeOffset(2026,11,2,9,0,0,TimeSpan.Zero);
   async Task<HttpClient> Login(string name){
    var token=await Send(anonymous,HttpMethod.Post,"/auth/token",new{login=name,password=Password});
    var client=factory.CreateClient(new(){HandleCookies=false});
    client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token.GetProperty("accessToken").GetString());
    return client;
   }
   var administrator=await Send(owner,HttpMethod.Post,"/owner/employees",new{userName="inv.admin",email="inv.admin@example.test",fullName="Administrador sintético",password=Password});
   using var admin=await Login("inv.admin");
   await Send(owner,HttpMethod.Post,"/platform/businesses",new{name="Convite QA",slug="convite-qa",ownerUsername="inv.admin"});
   admin.DefaultRequestHeaders.Add("X-Orbe-Business","convite-qa");
   var invite=await Send(admin,HttpMethod.Get,"/owner/invitation");var code=invite.GetProperty("code").GetString();
   Check(code?.Length==24,"convite gerado para empresa existente");
   await Send(admin,HttpMethod.Get,"/platform/invitations",expected:403);
   var companies=await Send(owner,HttpMethod.Get,"/platform/invitations");
   var business=companies.EnumerateArray().Single(b=>b.GetProperty("slug").GetString()=="convite-qa");
   var businessId=business.GetProperty("id").GetInt32();
   await Send(admin,HttpMethod.Post,$"/platform/invitations/{businessId}/rotate",new{},403);
   await Send(anonymous,HttpMethod.Post,"/auth/register",new{fullName="Profissional convidado",email="inv.prof@example.test",phone="11999999999",birthDate="2000-01-01",password=Password,username="inv.prof",invitationCode="INVALID"},400);
   await Send(anonymous,HttpMethod.Post,"/auth/register",new{fullName="Profissional convidado",email="inv.prof@example.test",phone="11999999999",birthDate="2000-01-01",password=Password,username="inv.prof",invitationCode=code},202);
   var mail=factory.Mail.Items.Last(m=>m.To=="inv.prof@example.test");
   var link=new Uri(mail.Body[mail.Body.IndexOf("http",StringComparison.Ordinal)..]);var query=QueryHelpers.ParseQuery(link.Query);
   await Send(anonymous,HttpMethod.Post,"/auth/confirm?userId="+Uri.EscapeDataString(query["userId"].ToString())+"&token="+Uri.EscapeDataString(query["token"].ToString()),new{});
   using var professional=await Login("inv.prof");
   Check((await Send(professional,HttpMethod.Get,"/businesses/mine")).GetArrayLength()==0,"convite e e-mail confirmado não liberam acesso antes de aprovação");
   var rotated=await Send(owner,HttpMethod.Post,$"/platform/invitations/{businessId}/rotate",new{});
   await Send(professional,HttpMethod.Post,"/memberships/request",new{code},400);
   var requests=await Send(admin,HttpMethod.Get,"/owner/membership-requests");
   var requestId=Id(requests.EnumerateArray().Single());
   await Send(owner,HttpMethod.Put,"/owner/membership-requests/"+requestId,new{approve=true},404);
   await Send(admin,HttpMethod.Put,"/owner/membership-requests/"+requestId,new{approve=true},204);
   await Send(admin,HttpMethod.Put,"/owner/membership-requests/"+requestId,new{approve=true},409);
   Check((await Send(professional,HttpMethod.Get,"/businesses/mine")).GetArrayLength()==1,"aprovação mantém solicitação anterior à troca e cria exatamente um vínculo");
   professional.DefaultRequestHeaders.Add("X-Orbe-Business","convite-qa");
   var me=await Send(professional,HttpMethod.Get,"/auth/me");var employee=me.GetProperty("employeeId").GetString();
   await Send(professional,HttpMethod.Get,"/owner/membership-requests",expected:403);
   var category=await Send(professional,HttpMethod.Post,"/team/categories",new{name="Categoria QA"});
   var categoryId=Id(category);
   var service=await Send(professional,HttpMethod.Post,"/team/services",new{name="Serviço QA",durationMinutes=30,priceCents=1000,enabled=true,categoryId});
   await Send(professional,HttpMethod.Put,"/team/categories/"+categoryId,new{name="Nova categoria",expectedName="Categoria QA"});
   await Send(professional,HttpMethod.Put,"/team/categories/"+categoryId,new{name="Alteração antiga",expectedName="Categoria QA"},409);
   Check((await Send(professional,HttpMethod.Get,"/public/services?employeeId="+employee))[0].GetProperty("categoryName").GetString()=="Nova categoria","renomear categoria atualiza o catálogo público");
   await Send(admin,HttpMethod.Delete,"/team/categories/"+categoryId+"?employeeId="+Id(administrator),new{name="Nova categoria"},400);
   await Send(professional,HttpMethod.Delete,"/team/categories/"+categoryId,new{name="Nova categoria"},204);
   var services=await Send(professional,HttpMethod.Get,"/team/services");
   Check(services.GetArrayLength()==1&&services[0].GetProperty("categoryId").ValueKind==JsonValueKind.Null,"excluir categoria preserva serviço e remove somente sua classificação");
   foreach(var kind in new[]{"Folga","Compromisso","Outro"}){
    var series=Guid.NewGuid();
    var result=await Send(professional,HttpMethod.Post,"/team/recurring",new{seriesId=series,employeeId=employee,title="Fixo "+kind,kind,
     firstDate="2026-11-03",startTime="09:00",endTime="10:00",count=2,expectedCents=0,everyDays=13,notifyUnavailable=true});
    Check(result.GetProperty("pending").GetInt32()==2,kind+" aceita intervalo de 13 dias e fica pendente fora da jornada");
   }
   var notices=await Send(professional,HttpMethod.Get,"/notifications");
   Check(notices.GetProperty("items").EnumerateArray().Count(n=>n.GetProperty("kind").GetString()=="Review")==6,"avisos de revisão incluem todos os tipos de evento");
   var hours=await Send(professional,HttpMethod.Get,"/team/hours");
   await Send(professional,HttpMethod.Put,"/team/hours",new{version=hours.GetProperty("version").GetGuid(),weekly=new[]{new{day=2,window=new{startMinute=540,endMinute=720}}},exceptions=Array.Empty<object>()});
   var events=await Send(professional,HttpMethod.Get,"/team/appointments");
   var pending=events.EnumerateArray().First(e=>e.GetProperty("kind").GetString()=="Folga");
   var id=Id(pending);
   await Send(professional,HttpMethod.Put,"/team/blocks/"+id,new{employeeId=employee,title="Folga revisada",kind="Folga",start="2026-11-03T09:00:00-03:00",end="2026-11-03T10:00:00-03:00",version=pending.GetProperty("version").GetGuid()});
   factory.Clock.Now=new DateTimeOffset(2026,11,2,12,0,0,TimeSpan.Zero);
   notices=await Send(professional,HttpMethod.Get,"/notifications");
   Check(notices.GetProperty("items").EnumerateArray().Any(n=>n.GetProperty("appointmentId").GetString()==id&&n.GetProperty("kind").GetString()=="Day"),"folga remarcada confirma e recebe lembrete um dia antes");
  }finally{factory.Clock.Now=previous;factory.Mail.Ready=mailReady;}
 }
}
