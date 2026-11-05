#if DEBUG
using System.Net;
using System.Net.Http.Json;
using Agenda.Core;
namespace Agenda.App;
internal sealed class DebugNotificationFixture:HttpMessageHandler
{
 internal static CloudApi Api()=>new("http://localhost",new DebugNotificationFixture());
 private readonly List<CloudNotice> rows=[new(Guid.NewGuid(),"qa-1","Booking",DateTimeOffset.UtcNow.AddDays(2),DateTimeOffset.UtcNow,null,"Atendimento de teste","Cliente sintética"),new(Guid.NewGuid(),"qa-2","Hour",DateTimeOffset.UtcNow.AddHours(1),DateTimeOffset.UtcNow,null,"Serviço de teste","Cliente sintética")];
 protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
 {
  var parts=request.RequestUri!.AbsolutePath.Split('/');if(parts.Length>4&&Guid.TryParse(parts[4],out var id)){
   var index=rows.FindIndex(x=>x.Id==id);if(index>=0){if(request.Method==HttpMethod.Delete)rows.RemoveAt(index);else rows[index]=rows[index] with{ReadAt=DateTimeOffset.UtcNow};}
   return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
  }
  return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=JsonContent.Create(new CloudInbox(rows.ToArray(),rows.Count(x=>x.ReadAt is null),rows.Count))});
 }
}
public sealed partial class DashboardPage
{
 internal async Task QaNotificationCheck(bool actions)
 {
  tab="Notificações";await LoadNotifications();if(inbox.Items.Length!=2||inbox.UnreadCount!=2||!notificationBadges.Any(b=>b.IsVisible))throw new Exception("Unread badge/list missing");
  if(actions){await NoticeAction(inbox.Items[0],false);if(inbox.UnreadCount!=1)throw new Exception("Read count not updated");await NoticeAction(inbox.Items.Single(x=>x.ReadAt is null),true);if(inbox.UnreadCount!=0||inbox.Items.Length!=1||notificationBadges.Any(b=>b.IsVisible))throw new Exception("Delete/badge not updated");}
  System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(),$"orbe-notifications-{Environment.ProcessId}.json"),System.Text.Json.JsonSerializer.Serialize(new{passed=true,actions,unread=inbox.UnreadCount,items=inbox.Items.Length}));
 }
}
#endif
