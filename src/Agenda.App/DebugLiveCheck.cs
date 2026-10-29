#if DEBUG && WINDOWS
using Agenda.Core;

namespace Agenda.App;
public partial class App
{
 private static async Task RunLiveCheck(Window window)
 {
  var output=Path.Combine(Path.GetTempPath(),$"orbe-live-check-{Environment.ProcessId}");
  try
  {
   var url=Environment.GetEnvironmentVariable("ORBE_QA_URL")??"";
   if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||!uri.IsLoopback)throw new InvalidOperationException("QA requires loopback");
   var api=new CloudApi(url){AccessToken=Environment.GetEnvironmentVariable("ORBE_QA_TOKEN")};var profile=await api.Me();
   if(!profile.Owner||!profile.Email.EndsWith("@example.test",StringComparison.Ordinal))throw new InvalidOperationException("QA requires synthetic profile");
   var page=new DashboardPage(api,profile);window.Page=Navigation(page);
   for(int i=0;i<30&&!page.QaDataReady;i++)await Task.Delay(250);
   if(!page.QaDataReady)throw new InvalidOperationException("QA data not ready");
   page.QaSelectFirst();await Task.Delay(700);await Capture("initial");
   await Task.Delay(25000);await Capture("final");Environment.Exit(page.QaAppointmentCount==0?0:1);
   async Task Capture(string name)
   {
    if(window.Handler?.PlatformView is not Microsoft.UI.Xaml.Window native||native.Content is not Microsoft.UI.Xaml.FrameworkElement root)throw new InvalidOperationException("QA window missing");
    await StartupCapture.Capture(root,output+"-"+name+".png");
    File.WriteAllText(output+"-"+name+".json",System.Text.Json.JsonSerializer.Serialize(new{count=page.QaAppointmentCount}));
   }
  }
  catch(Exception e){File.WriteAllText(output+"-error.txt",e.GetType().Name);Environment.Exit(1);}
 }
}
#endif
