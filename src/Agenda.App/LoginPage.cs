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
 private readonly Label message=Text("",12,Coral);
 private readonly ActivityIndicator spinner=new(){Color=Violet,IsVisible=false,HeightRequest=24};
 private readonly Grid presentation=new(){ColumnSpacing=40};
 private readonly Border loginCard;
 private readonly View introduction;
 private bool? widePresentation;
 private bool restored,busy;
 public LoginPage()
 {
  Title="Orbe";BackgroundColor=CanvasColor;SafeAreaEdges=SafeAreaEdges.All;NavigationPage.SetHasNavigationBar(this,false);
  password.IsPassword=true;password.MaxLength=user.MaxLength=256;
  SemanticProperties.SetDescription(user,"Usuário ou e-mail");SemanticProperties.SetDescription(password,"Senha");
  var panel=Stack(14,Row(10,BrandImage(40),Text("orbe",30,Ink,true)),
   Pill("SEU ESPAÇO DE TRABALHO",Mint),Stack(8,Text("Tudo no seu tempo.",28,Ink,true),Text("Entre para cuidar da sua agenda, da sua disponibilidade e das suas finanças.",14,Muted)),
   Field("Usuário ou e-mail",user),Field("Senha",password),Button("Entrar",()=>Attempt(false),primary:true),Row(8,spinner,message),Rule(),
   Text("Quer reservar um horário?",14,Ink,true),Button("Entrar como cliente",()=>Portal("/")),Button("Esqueci minha senha",()=>Portal("/recuperar-senha"),quiet:true),
   Text("Seu acesso é pessoal. A disponibilidade de cada profissional é compartilhada com o site em tempo real.",11,Dim));
  loginCard=Card(panel,28);loginCard.MaximumWidthRequest=540;loginCard.HorizontalOptions=LayoutOptions.Fill;loginCard.VerticalOptions=LayoutOptions.Center;
#if ANDROID
  panel.Children.Insert(panel.Children.Count-1,Button("Atualizar aplicativo",async()=>await Navigation.PushAsync(new UpdatePage()),quiet:true));
#endif
  foreach(var button in panel.Children.OfType<Button>()){button.HorizontalOptions=LayoutOptions.Fill;button.MaximumWidthRequest=double.PositiveInfinity;}
  panel.Add(Text("Orbe · "+AppInfo.Current.VersionString+" · compilação "+AppInfo.Current.BuildString,10,Dim));
  var heroTitle=Text("Seu negócio.\nNo seu ritmo.",60,Ink,true);heroTitle.MaximumWidthRequest=950;heroTitle.HorizontalOptions=LayoutOptions.Start;
  var heroDescription=Text("Um espaço para organizar seus atendimentos, cuidar da equipe e acompanhar cada resultado.",20,Muted);heroDescription.MaximumWidthRequest=720;heroDescription.HorizontalOptions=LayoutOptions.Start;
  introduction=Stack(30,Pill("AGENDA, EQUIPE E FINANÇAS",Mint),heroTitle,heroDescription,Rule(),
   Row(18,Card(Icon("calendar",Violet,28),16,Raised),Stack(5,Text("Sua rotina em perspectiva",19,Ink,true),Text("Calendário, jornadas e horários de cada profissional.",14,Muted))),
   Row(18,Card(Icon("chart",Mint,28),16,Raised),Stack(5,Text("Clareza para decidir",19,Ink,true),Text("Recebimentos, despesas e resultados em um só lugar.",14,Muted))),
   Row(18,Card(Icon("people",Blue,28),16,Raised),Stack(5,Text("Todos conectados",19,Ink,true),Text("Aplicativo e reservas online compartilham a mesma agenda.",14,Muted))));
  introduction.VerticalOptions=LayoutOptions.Center;introduction.MaximumWidthRequest=900;introduction.HorizontalOptions=LayoutOptions.Center;
  Content=new ScrollView{Content=presentation};SizeChanged+=(_,_)=>ArrangePresentation();ArrangePresentation();
  password.Completed+=async(_,_)=>await Attempt(false);
 }
 private void ArrangePresentation()
 {
  presentation.MinimumHeightRequest=Math.Max(0,Height-16);var wide=Width>=1000;
  loginCard.MaximumWidthRequest=wide?double.PositiveInfinity:540;loginCard.VerticalOptions=wide?LayoutOptions.Fill:LayoutOptions.Center;
  loginCard.Padding=wide?new Thickness(Math.Clamp(Width*.035,32,100),40):new Thickness(28);
  if(loginCard.Content is View fields){fields.MaximumWidthRequest=wide?720:double.PositiveInfinity;fields.VerticalOptions=LayoutOptions.Center;}
  if(widePresentation==wide)return;widePresentation=wide;
  presentation.Children.Clear();presentation.ColumnDefinitions.Clear();presentation.RowDefinitions.Clear();
  presentation.Padding=wide?new Thickness(48,32):new Thickness(18,24);
  if(wide){presentation.ColumnDefinitions.Add(new(new GridLength(1.25,GridUnitType.Star)));presentation.ColumnDefinitions.Add(new(GridLength.Star));presentation.Add(introduction);presentation.Add(loginCard,1);}
  else{presentation.ColumnDefinitions.Add(new(GridLength.Star));presentation.Add(loginCard);}
 }
 protected override async void OnAppearing()
 {base.OnAppearing();if(restored)return;restored=true;
#if DEBUG
  if(Environment.GetCommandLineArgs().Contains("--live-check"))return;
#endif
  if(!Environment.GetCommandLineArgs().Contains("--startup-check"))await Attempt(true);}
 private async Task Portal(string path)
 {try{var connection=await CloudRouting.Discover();if(!await Launcher.Default.OpenAsync(new Uri(connection.ApiUrl+path)))message.Text="Não foi possível abrir o navegador.";}catch(ArgumentException e){message.Text=e.Message;}catch(Exception){message.Text="Não foi possível abrir o navegador. Tente novamente.";}}
 private async Task Attempt(bool restore)
 {
  if(busy)return;busy=true;spinner.IsVisible=spinner.IsRunning=true;CloudApi? api=null;
  try
  {
   var token=restore?await SecureStorage.Default.GetAsync(CloudSessionStore.TokenKey):null;if(restore&&string.IsNullOrEmpty(token))return;
   if(!restore&&(string.IsNullOrWhiteSpace(user.Text)||string.IsNullOrEmpty(password.Text))){message.Text="Informe seu usuário e sua senha.";return;}
   var connection=await CloudRouting.Discover();
   if(restore&&Preferences.Default.Get(CloudSessionStore.UrlKey,CloudApi.DefaultUrl)!=connection.ApiUrl){SecureStorage.Default.Remove(CloudSessionStore.TokenKey);message.Text="A conexão do Orbe foi atualizada pelo administrador. Entre novamente.";return;}
   api=new(connection.ApiUrl);CloudProfile profile;
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
