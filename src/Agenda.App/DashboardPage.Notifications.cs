using Agenda.Core;
using static Agenda.App.OrbeUi;

namespace Agenda.App;

public sealed partial class DashboardPage
{
 private CloudInbox inbox=new([],0,0);
 private readonly List<Border> notificationBadges=[];
 private bool notificationLoading,notificationLoaded;
 private string? notificationError;
 private int notificationLimit=50;
 private View WithNotificationBadge(View view)
 {
  var badge=Card(Text(inbox.UnreadCount>99?"99+":inbox.UnreadCount.ToString(),10,Colors.White,true),4,Color.FromArgb("#DC4560"),Colors.Transparent,12);
  badge.IsVisible=inbox.UnreadCount>0;badge.InputTransparent=true;badge.HorizontalOptions=LayoutOptions.End;badge.VerticalOptions=LayoutOptions.Start;badge.Margin=new Thickness(0,0,2,0);
  notificationBadges.Add(badge);var grid=new Grid();grid.Add(view);grid.Add(badge);return grid;
 }
 private void UpdateNotificationBadges()
 {
  foreach(var badge in notificationBadges){badge.IsVisible=inbox.UnreadCount>0;if(badge.Content is Label label)label.Text=inbox.UnreadCount>99?"99+":inbox.UnreadCount.ToString();}
  foreach(var pair in navigationButtons.Where(x=>x.Value=="Notificações"))SemanticProperties.SetDescription(pair.Key,$"Notificações, {inbox.UnreadCount} não lidas");
 }
 private async Task LoadNotifications(bool render=true)
 {
  if(notificationLoading)return;notificationLoading=true;
  try{
   var page=await client.Get<CloudInbox>($"notifications?limit={Math.Min(100,notificationLimit)}");
   // Preserve all pages when a list exceeds the server page size.
   var rows=page.Items.ToList();for(int offset=rows.Count;offset<Math.Min(notificationLimit,page.Total);offset+=100){var next=await client.Get<CloudInbox>($"notifications?offset={offset}&limit=100");rows.AddRange(next.Items.Take(notificationLimit-rows.Count));if(next.Items.Length==0)break;}
   var changed=!inbox.Items.SequenceEqual(rows)||inbox.UnreadCount!=page.UnreadCount||inbox.Total!=page.Total||notificationError is not null;
   inbox=page with{Items=rows.ToArray()};notificationLoaded=true;notificationError=null;UpdateNotificationBadges();if(render&&tab=="Notificações"&&changed)RenderNow();
  }
  catch(CloudException e)when(e.Status==System.Net.HttpStatusCode.Unauthorized){await CloudSessionStore.End(this,client);}
  catch(Exception){notificationError="Não foi possível atualizar os avisos. Tente novamente.";if(render&&tab=="Notificações")RenderNow();}
  finally{notificationLoading=false;}
 }
 private async Task NoticeAction(CloudNotice notice,bool delete)
 {
  if(delete)await client.Delete($"notifications/{notice.Id}",new{});else await client.Put<System.Text.Json.JsonElement>($"notifications/{notice.Id}/read",new{});
  await LoadNotifications();
 }
 private void RenderNotifications()
 {
  body.Add(Stack(8,Text("Notificações",compact?28:32,Ink,true),Text(inbox.UnreadCount==0?"Seus agendamentos e lembretes, em um só lugar.":$"Você tem {inbox.UnreadCount} notificação(ões) não lida(s).",14,Muted)));
  body.Add(Button("Atualizar avisos",()=>LoadNotifications()));
  if(notificationError is not null)body.Add(Text(notificationError,13,Coral));
  if(!notificationLoaded){body.Add(Text("Carregando seus avisos…",14,Muted));return;}
  if(inbox.Items.Length==0){body.Add(Card(Stack(16,Icon("bell",Violet,40),Text("Tudo em dia por aqui",22,Ink,true),Text("Novos agendamentos e lembretes de 1 dia e 1 hora antes aparecerão aqui.",14,Muted)),28));return;}
  foreach(var notice in inbox.Items)
  {
   var title=notice.Kind switch{"Day"=>"Lembrete · 1 dia antes","Hour"=>"Lembrete · 1 hora antes","Review"=>"Agendamento fixo precisa ser remarcado","Cancellation"=>"Agendamento cancelado pela cliente",_=>"Agendamento confirmado"};
   var content=Stack(12,Between(Text(title,16,Ink,true),Pill(notice.ReadAt is null?"NOVA":"LIDA",notice.ReadAt is null?Coral:Muted)),Text(notice.Subject,18,Ink,true));
   if(!string.IsNullOrWhiteSpace(notice.Person))content.Add(Text(notice.Person,13,Muted));
   content.Add(Text($"Atendimento: {client.Local(notice.OccurrenceStart):dd/MM/yyyy 'às' HH:mm}",14,Ink));
   content.Add(Text($"Aviso de {client.Local(notice.DeliverAt):dd/MM/yyyy HH:mm}",11,Dim));
   var actions=Stack(8);
   if(notice.Kind=="Review"&&notice.EmployeeId is {} employee)
   {
    actions.Add(Button("Remarcar ocorrência",()=>Guard(async()=>{
     var scope=new CloudAgendaRepository(client,employee);
     var pending=await scope.PendingAppointment(notice.AppointmentId);
     if(pending is null){await LoadNotifications();return;}
     await OpenAppointment(pending,scope);
    }),primary:true));
   }
   if(WhatsAppUri(notice.ClientPhone,true) is {} whatsapp)
    actions.Add(Button("WhatsApp da cliente",()=>Guard(async()=>await Launcher.Default.OpenAsync(whatsapp)),quiet:true));
   if(notice.ReadAt is null)actions.Add(Button("Marcar como lida",()=>Guard(()=>NoticeAction(notice,false))));
   actions.Add(Button("Excluir",()=>Guard(()=>NoticeAction(notice,true)),quiet:true));
   content.Add(actions);body.Add(Card(content,20,stroke:notice.ReadAt is null?Violet:Line));
  }
  if(inbox.Items.Length<inbox.Total)body.Add(Button("Carregar mais avisos",async()=>{notificationLimit+=50;await LoadNotifications();}));
 }
}
