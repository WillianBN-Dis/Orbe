using Agenda.Core;
using static Agenda.App.OrbeUi;

namespace Agenda.App;

public sealed partial class DashboardPage
{
 private static Uri? WhatsAppUri(string? value,bool assumeBrazil=false)
 {
  var phone=new string((value??"").Where(char.IsAsciiDigit).ToArray());
  if(assumeBrazil&&value?.TrimStart().StartsWith('+')!=true&&phone.Length is 10 or 11)phone="55"+phone;
  return phone.Length is >=10 and <=15&&phone[0]!='0'?new Uri("https://wa.me/"+phone):null;
 }

 private async Task OpenSupport()
 {
  var contacts=await client.Get<CloudSupport>("support");
  var page=new ContentPage{Title="Suporte",BackgroundColor=CanvasColor};
  var content=Stack(16,Button("‹ Voltar",async()=>await Navigation.PopAsync(),quiet:true),Text("Suporte",28,Ink,true));
  var actions=Stack(12);
  var phone=Input("5511999999999",contacts.WhatsApp);
  phone.Keyboard=Keyboard.Telephone;
  var email=Input("E-mail de suporte",contacts.Email);
  email.Keyboard=Keyboard.Email;
  void RenderContacts(CloudSupport current)
  {
   actions.Children.Clear();
   if(WhatsAppUri(current.WhatsApp) is {} whatsapp)
    actions.Add(Button("Falar pelo WhatsApp",async()=>await Launcher.Default.OpenAsync(whatsapp)));
   if(!string.IsNullOrEmpty(current.Email))
    actions.Add(Button("Enviar e-mail",async()=>await Launcher.Default.OpenAsync(new Uri("mailto:"+Uri.EscapeDataString(current.Email)))));
   if(actions.Children.Count==0)actions.Add(Text("Os canais de suporte ainda estão em configuração.",14,Muted));
  }
  RenderContacts(contacts);
  content.Add(Card(actions));
  if(profile.Owner)
  {
   var feedback=Text("",13,Muted);
   content.Add(Card(Stack(12,Field("WhatsApp com país e DDD",phone),Field("E-mail de suporte",email),
    Button("Salvar canais de suporte",async()=>{
     try{
      var saved=await client.Put<CloudSupport>("support",new CloudSupport(phone.Text,email.Text));
      RenderContacts(saved);feedback.Text="Canais atualizados.";
     }catch(Exception e){feedback.Text=FriendlyError(e);}
    },primary:true),feedback)));
  }
  page.Content=new ScrollView{Content=content,Padding=24,MaximumWidthRequest=680};
  NavigationPage.SetHasNavigationBar(page,false);
  await Navigation.PushAsync(page);
 }
}
