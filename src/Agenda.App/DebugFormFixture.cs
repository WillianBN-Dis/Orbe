#if DEBUG
using System.Net;
using System.Text;
using System.Text.Json;
using Agenda.Core;

namespace Agenda.App;
internal static class DebugFormFixture
{
 internal static Page Create()
 {
  var api=new CloudApi("http://localhost",new FixtureHandler());
  var profile=new CloudProfile("qa","Equipe QA","qa@example.test",null,null,true,["Owner"],"a");
  return Environment.GetEnvironmentVariable("ORBE_QA_FORM") switch{
   "services"=>AccountPages.Services(api),"block"=>AccountPages.Block(api,"a"),_=>AccountPages.Hours(api,profile,"a")
  };
 }
 private static IEnumerable<T> Find<T>(IVisualTreeElement root)where T:class
 {if(root is T result)yield return result;foreach(var child in root.GetVisualChildren())foreach(var match in Find<T>(child))yield return match;}
 internal static async Task Verify(Page page)
 {
  for(var i=0;i<30&&!Find<Button>(page).Any();i++)await Task.Delay(100);
  var mode=Environment.GetEnvironmentVariable("ORBE_QA_FORM")??"hours";
  if(mode=="hours"){
   Find<Button>(page).Single(x=>x.Text=="+ Jornada · manhã e tarde").SendClicked();
   var clocks=Find<TimePicker>(page).ToArray();if(clocks.Length!=4)throw new Exception("Morning/afternoon selectors missing");
   var boxes=Find<CheckBox>(page).ToArray();boxes[2].IsChecked=true;
   if(clocks[2].IsEnabled||clocks[3].IsEnabled||!clocks[0].IsEnabled)throw new Exception("Period checkbox did not isolate afternoon");
   if(clocks[0].Time!=TimeSpan.FromHours(9)||clocks[1].Time!=TimeSpan.FromHours(12))throw new Exception("Morning defaults wrong");
  }else if(mode=="block"){
   var period=Find<Picker>(page).Single(x=>x.ItemsSource?.Count==4);period.SelectedIndex=2;
   var clocks=Find<TimePicker>(page).ToArray();if(clocks.Length!=2||clocks[0].Time!=TimeSpan.FromHours(13)||clocks[1].Time!=TimeSpan.FromHours(18))throw new Exception("Afternoon block preset wrong");
  }else{
   if(!Find<Button>(page).Any(x=>x.Text=="Editar serviço")||!Find<Button>(page).Any(x=>x.Text=="Excluir serviço"))throw new Exception("Service CRUD missing");
  }
  File.WriteAllText(Path.Combine(Path.GetTempPath(),$"orbe-forms-{Environment.ProcessId}.json"),JsonSerializer.Serialize(new{passed=true,mode}));
 }
 private sealed class FixtureHandler:HttpMessageHandler
 {
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
  {
   if(request.RequestUri?.IsLoopback!=true)throw new Exception("QA loopback required");
   var path=request.RequestUri.AbsolutePath;var version=Guid.Parse("11111111-1111-1111-1111-111111111111");
   object value=path switch{
    "/api/v1/auth/me"=>new{id="qa",fullName="Equipe QA",email="qa@example.test",emailConfirmed=true,roles=new[]{"Owner"},employeeId="a"},
    "/api/v1/team/employees"=>new[]{new{id="a",displayName="Maria Silva",enabled=true,version},new{id="b",displayName="Ana Souza",enabled=true,version}},
    "/api/v1/team/hours"=>new{employeeId="a",version,weekly=Array.Empty<object>(),exceptions=Array.Empty<object>()},
    "/api/v1/team/services"=>new[]{new{id="service-a",employeeId="a",employeeName="Maria Silva",name="Atendimento personalizado",durationMinutes=30,priceCents=5000,enabled=true,version}},
    _=>throw new Exception("Unexpected fixture route")
   };
   return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(value),Encoding.UTF8,"application/json")});
  }
 }
}
#endif
