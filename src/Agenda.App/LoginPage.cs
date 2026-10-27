using Agenda.Core;
using System.Net;
using static Agenda.App.OrbeUi;

namespace Agenda.App;
internal static class CloudSessionStore
{
 public const string TokenKey="orbe-cloud-token",UrlKey="orbe-cloud-url";
 public static void Show(Page page,Page next){var window=page.Window??Application.Current!.Windows[0];window.Page=App.Navigation(next);}
 public static async Task End(Page page,CloudApi api)
 {try{await api.Post<System.Text.Json.JsonElement>("auth/logout",new{});}catch(CloudException){}finally{SecureStorage.Default.Remove(TokenKey);api.Dispose();Show(page,new LoginPage());}}
}
public sealed class LoginPage:ContentPage
{
 private readonly Entry user=Input("Seu usuário ou e-mail"),password=Input("Sua senha");
 private readonly string server=Preferences.Default.Get(CloudSessionStore.UrlKey,CloudApi.DefaultUrl);
 private readonly Label message=Text("",12,Coral);
 private readonly ActivityIndicator spinner=new(){Color=Violet,IsVisible=false,HeightRequest=24};
 private bool restored,busy;
 public LoginPage()
 {
  Title="Orbe";BackgroundColor=CanvasColor;SafeAreaEdges=SafeAreaEdges.All;NavigationPage.SetHasNavigationBar(this,false);
  password.IsPassword=true;password.MaxLength=user.MaxLength=256;
  SemanticProperties.SetDescription(user,"Usuário ou e-mail");SemanticProperties.SetDescription(password,"Senha");
  var panel=Stack(14,Row(12,new Image{Source="orbe.png",HeightRequest=52,WidthRequest=52},Text("orbe",36,Ink,true)),
   Pill("SEU ESPAÇO DE TRABALHO",Mint),Stack(8,Text("Tudo no seu tempo.",28,Ink,true),Text("Entre para cuidar da sua agenda, da sua disponibilidade e das suas finanças.",14,Muted)),
   Field("Usuário ou e-mail",user),Field("Senha",password),Button("Entrar",()=>Attempt(false),primary:true),Row(8,spinner,message),Rule(),
   Text("Quer reservar um horário?",14,Ink,true),Button("Entrar como cliente",()=>Portal("/")),Button("Esqueci minha senha",()=>Portal("/recuperar-senha"),quiet:true),
   Text("Seu acesso é pessoal. A disponibilidade de cada profissional é compartilhada com o site em tempo real.",11,Dim));
  var card=Card(panel,24);card.MaximumWidthRequest=510;card.HorizontalOptions=LayoutOptions.Center;card.VerticalOptions=LayoutOptions.Center;
#if ANDROID
  panel.Children.Insert(panel.Children.Count-1,Button("Atualizar aplicativo",async()=>await Navigation.PushAsync(new UpdatePage()),quiet:true));
#endif
  foreach(var button in panel.Children.OfType<Button>()){button.HorizontalOptions=LayoutOptions.Fill;button.MaximumWidthRequest=double.PositiveInfinity;}
  Content=new ScrollView{Content=new VerticalStackLayout{Padding=new Thickness(18,28),Children={card}}};
  password.Completed+=async(_,_)=>await Attempt(false);
 }
 protected override async void OnAppearing()
 {base.OnAppearing();if(restored)return;restored=true;if(!Environment.GetCommandLineArgs().Contains("--startup-check"))await Attempt(true);}
 private async Task Portal(string path)
 {try{using var check=new CloudApi(server);if(!await Launcher.Default.OpenAsync(new Uri(check.BaseUrl+path)))message.Text="Não foi possível abrir o navegador.";}catch(ArgumentException e){message.Text=e.Message;}catch(Exception){message.Text="Não foi possível abrir o navegador. Tente novamente.";}}
 private async Task Attempt(bool restore)
 {
  if(busy)return;busy=true;spinner.IsVisible=spinner.IsRunning=true;CloudApi? api=null;
  try
  {
   var token=restore?await SecureStorage.Default.GetAsync(CloudSessionStore.TokenKey):null;if(restore&&string.IsNullOrEmpty(token))return;
   if(!restore&&(string.IsNullOrWhiteSpace(user.Text)||string.IsNullOrEmpty(password.Text))){message.Text="Informe seu usuário e sua senha.";return;}
   api=new(server);CloudProfile profile;
   if(restore){api.AccessToken=token;profile=await api.Me();}
   else{var session=await api.Login(user.Text!.Trim(),password.Text!);api.AccessToken=session.AccessToken;profile=session.Profile;password.Text="";}
   if(!profile.Team){await api.Post<System.Text.Json.JsonElement>("auth/logout",new{});SecureStorage.Default.Remove(CloudSessionStore.TokenKey);message.Text="Sua conta é de cliente. Use Entrar como cliente para acessar o site.";return;}
   await api.ConfigureZone();await SecureStorage.Default.SetAsync(CloudSessionStore.TokenKey,api.AccessToken!);Preferences.Default.Set(CloudSessionStore.UrlKey,api.BaseUrl);
   CloudSessionStore.Show(this,new DashboardPage(api,profile));api=null;
  }
  catch(CloudException e){message.Text=e.Message;if(e.Status==HttpStatusCode.Unauthorized)SecureStorage.Default.Remove(CloudSessionStore.TokenKey);}
  catch(ArgumentException e){message.Text=e.Message;}
  catch(Exception){message.Text="Não foi possível abrir sua sessão com segurança. Tente novamente.";}
  finally{api?.Dispose();busy=false;spinner.IsRunning=spinner.IsVisible=false;}
 }
}
