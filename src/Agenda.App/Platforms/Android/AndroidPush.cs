#if ORBE_FIREBASE
using Agenda.Core;
using Android.App;
using Android.Content;
using Android.OS;
using Firebase.Messaging;
using Android.Gms.Extensions;

namespace Agenda.App;

internal static class AndroidPush
{
 internal const string Channel="orbe-inbox";
 private static CloudApi? api;
 internal static async void Connect(CloudApi client)
 {
  if(ReferenceEquals(api,client))return;
  try
  {
   if(OperatingSystem.IsAndroidVersionAtLeast(33)&&
      await Permissions.RequestAsync<Permissions.PostNotifications>()!=PermissionStatus.Granted)return;
   CreateChannel();
   api=client;
   var pending=FirebaseMessaging.Instance.GetToken();
   await pending.AsAsync();
   var token=pending.Result;
   await Register(token?.ToString());
  }
  catch(Exception){api=null;}
 }
 internal static async Task Register(string? token)
 {
  if(api is {} client&&!string.IsNullOrWhiteSpace(token))
   await client.Put<System.Text.Json.JsonElement>("team/push-device",new{token});
 }
 internal static async Task Disconnect()
 {
  api=null;
  try{await FirebaseMessaging.Instance.DeleteToken().AsAsync();}catch(Exception){}
  if(Android.App.Application.Context.GetSystemService(Context.NotificationService) is NotificationManager manager)manager.CancelAll();
 }
 private static NotificationManager? CreateChannel()
 {
  var manager=Android.App.Application.Context.GetSystemService(Context.NotificationService) as NotificationManager;
  if(OperatingSystem.IsAndroidVersionAtLeast(26))manager?.CreateNotificationChannel(new NotificationChannel(Channel,"Agendamentos do Orbe",NotificationImportance.Default));
  return manager;
 }
 internal static void Show(string id,string message)
 {
  var context=Android.App.Application.Context;
  var manager=CreateChannel();
  var intent=new Intent(context,typeof(MainActivity)).SetAction("br.com.agendafinanceira.INBOX."+id);
  intent.PutExtra("orbe_notifications","1");
  var flags=PendingIntentFlags.UpdateCurrent;
  if(OperatingSystem.IsAndroidVersionAtLeast(23))flags|=PendingIntentFlags.Immutable;
  var launch=PendingIntent.GetActivity(context,0,intent,flags);
  var notification=new AndroidX.Core.App.NotificationCompat.Builder(context,Channel)
   .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
   .SetContentTitle("Orbe").SetContentText(message).SetContentIntent(launch).SetAutoCancel(true).Build();
  manager?.Notify(id,0,notification);
 }
}

[Service(Exported=false)]
[IntentFilter(["com.google.firebase.MESSAGING_EVENT"])]
public sealed class OrbeMessagingService:FirebaseMessagingService
{
 public override async void OnNewToken(string token)
 {
  base.OnNewToken(token);
  try{await AndroidPush.Register(token);}catch(Exception){}
 }
 public override void OnMessageReceived(RemoteMessage message)
 {
  if(message.Data.TryGetValue("orbe_notifications",out var route)&&route=="1"&&
   message.Data.TryGetValue("notice_id",out var id)&&Guid.TryParse(id,out _))
   AndroidPush.Show(id,message.GetNotification()?.Body??"Você tem um aviso no Orbe.");
 }
}
#endif
