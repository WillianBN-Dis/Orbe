using System.Net;
using System.Text.Json;
using Agenda.Core;
using static Agenda.App.OrbeUi;

namespace Agenda.App;
internal sealed class CloudFormPage:ContentPage
{
 internal readonly VerticalStackLayout Fields=new(){Spacing=18,Padding=22,MaximumWidthRequest=900,HorizontalOptions=LayoutOptions.Fill};
 private readonly ActivityIndicator spinner=new(){Color=Violet,HeightRequest=24,IsVisible=false};
 private readonly CloudApi api;
 private bool loaded,busy;
 internal Func<Task>? Load {get;set;}
 internal bool ReloadOnReturn {get;set;}
 internal CloudFormPage(CloudApi client,string title){api=client;Title=title;BackgroundColor=CanvasColor;SafeAreaEdges=SafeAreaEdges.All;var grid=new Grid{RowDefinitions=new(){new(GridLength.Auto),new(GridLength.Star)}};grid.Add(spinner);grid.Add(new ScrollView{Content=Fields},0,1);Content=grid;}
 protected override async void OnAppearing(){base.OnAppearing();if(loaded&&!ReloadOnReturn)return;loaded=true;if(Load is not null)await Run(Load);}
 internal async Task Run(Func<Task> action)
 {
  if(busy)return;busy=true;spinner.IsRunning=spinner.IsVisible=true;
  try{await action();}
  catch(CloudException e)when(e.Status==HttpStatusCode.Unauthorized){await CloudSessionStore.End(this,api);}
  catch(Exception e){if(Window is not null&&ReferenceEquals(Navigation.NavigationStack.LastOrDefault(),this))await DisplayAlertAsync("Vamos conferir?",e is CloudException or ArgumentException?e.Message:"Não foi possível concluir. Confira sua conexão e atualize antes de tentar novamente.","Entendi");}
  finally{busy=false;spinner.IsRunning=spinner.IsVisible=false;}
 }
}
internal static partial class AccountPages
{
 internal static Page Connection(CloudApi api)
 {
  var page=new CloudFormPage(api,"Conexão central do Orbe");var address=Input("Endereço HTTPS",api.BaseUrl);address.MaxLength=512;
  Orbe.Cloud.Core.SharedConnection? connection=null;page.Load=async()=>{connection=await api.Get<Orbe.Cloud.Core.SharedConnection>("public/connection");address.Text=connection.ApiUrl;};
  page.Fields.Add(Stack(18,Text("Uma conexão para todos",23,Ink,true),Text("Somente sua conta administrativa altera este endereço. PCs, celulares e site consultam a mesma configuração. O destino precisa estar conectado ao mesmo banco Orbe; esta ação não transfere dados.",13,Muted),Field("Aplicação Orbe na Discloud",address),Text("O endereço inicial de descoberta precisa continuar acessível para os aplicativos receberem as mudanças.",12,Muted),Button("Salvar para todos",()=>page.Run(async()=>{if(!(await api.Me()).Owner||connection is null)throw new ArgumentException("Somente WillianBN pode alterar a conexão.");var url=Orbe.Cloud.Core.ConnectionAddress.Normalize(Required(address,"o endereço"));if(!await page.DisplayAlertAsync("Alterar conexão para todos?","Todos os dispositivos receberão o endereço atualizado. Será necessário entrar novamente quando o endereço mudar.","Salvar para todos","Cancelar"))return;var result=await api.Put<Orbe.Cloud.Core.SharedConnection>("owner/connection",new{version=connection.Version,apiUrl=url});connection=result;if(result.ApiUrl==api.BaseUrl){await page.DisplayAlertAsync("Configuração salva","A conexão central foi confirmada para todos os dispositivos.","Entendi");await Back(page);}else await CloudSessionStore.End(page,api);}),primary:true)));
  return page;
 }
 private static string S(JsonElement j,string key)=>j.TryGetProperty(key,out var p)&&p.ValueKind==JsonValueKind.String?p.GetString()??"":"";
 private static bool B(JsonElement j,string key)=>j.TryGetProperty(key,out var p)&&p.ValueKind==JsonValueKind.True;
 private static int I(JsonElement j,string key)=>j.GetProperty(key).GetInt32();
 private static async Task Back(CloudFormPage page)=>await page.Navigation.PopAsync();
 private static string Required(Entry input,string name){if(string.IsNullOrWhiteSpace(input.Text))throw new ArgumentException("Informe "+name+".");return input.Text.Trim();}
 private static int Number(Entry input,string name){if(!int.TryParse(input.Text,out var number))throw new ArgumentException("Confira "+name+".");return number;}
 private static Picker People(CloudEmployee[] people,string? current=null)
 {var picker=new Picker{Title="Selecione o profissional",ItemsSource=people,ItemDisplayBinding=new Binding(nameof(CloudEmployee.Caption))};picker.SelectedIndex=Array.FindIndex(people,x=>x.Id==current);return picker;}
 private static string Picked(Picker picker)=>picker.SelectedItem is CloudEmployee person?person.Id:throw new ArgumentException("Selecione um profissional.");
 private static async Task<CloudEmployee[]> Active(CloudApi api)=>(await api.Get<CloudEmployee[]>("team/employees")).Where(x=>x.Enabled).ToArray();
 internal static Page Profile(CloudApi api,CloudProfile account)
 {
  var page=new CloudFormPage(api,"Minha conta");
  Entry name=Input("Nome completo",account.FullName),phone=Input("WhatsApp",account.Phone);phone.Keyboard=Keyboard.Telephone;
  var birthday=new DatePicker{Date=account.BirthDate?.ToDateTime(TimeOnly.MinValue),Format="dd/MM/yyyy"};
  var hasBirth=new Switch{IsToggled=account.BirthDate is not null};birthday.IsEnabled=hasBirth.IsToggled;hasBirth.Toggled+=(_,e)=>birthday.IsEnabled=e.Value;
  page.Fields.Add(Card(Stack(18,Text("Seu perfil",23,Ink,true),Field("Nome completo",name),Field("WhatsApp",phone),Between(Text("Informar nascimento",12,Muted),hasBirth),Field("Data de nascimento",birthday),Button("Salvar cadastro",()=>page.Run(async()=>{await api.Put<CloudProfile>("auth/me",new{fullName=Required(name,"seu nome"),phone=phone.Text??"",birthDate=hasBirth.IsToggled?DateOnly.FromDateTime(birthday.Date??DateTime.Today):(DateOnly?)null});await Back(page);}),primary:true))));
  page.Fields.Add(Card(Stack(16,Text("Acesso à conta",19,Ink,true),Text(account.Email,13,Muted),Button("Alterar e-mail",async()=>await page.Navigation.PushAsync(EmailPage(api))),Button("Trocar senha",async()=>await page.Navigation.PushAsync(PasswordPage(api))))));return page;
 }
 private static Page EmailPage(CloudApi api)
 {
  var page=new CloudFormPage(api,"Alterar e-mail");Entry address=Input("Novo e-mail"),password=Input("Senha atual");address.Keyboard=Keyboard.Email;password.IsPassword=true;
  page.Fields.Add(Stack(18,Text("Confirme seu novo endereço",23,Ink,true),Text("Enviaremos um link de confirmação. O endereço atual continua válido até a confirmação.",13,Muted),Field("Novo e-mail",address),Field("Senha atual",password),Button("Enviar confirmação",()=>page.Run(async()=>{await api.Post<JsonElement>("auth/email",new{email=Required(address,"o e-mail"),password=password.Text??""});password.Text="";await page.DisplayAlertAsync("Confira seu e-mail","Abra o link recebido para concluir a alteração e entre novamente.","Entendi");await Back(page);}),primary:true)));return page;
 }
 private static Page PasswordPage(CloudApi api)
 {
  var page=new CloudFormPage(api,"Trocar senha");Entry current=Input("Senha atual"),next=Input("Nova senha");current.IsPassword=next.IsPassword=true;current.MaxLength=next.MaxLength=256;
  page.Fields.Add(Stack(18,Text("Cuide do seu acesso",23,Ink,true),Field("Senha atual",current),Field("Nova senha",next,"Pelo menos 10 caracteres, letras maiúsculas, minúsculas e números."),Button("Trocar senha",()=>page.Run(async()=>{await api.Post<JsonElement>("auth/password",new{currentPassword=current.Text??"",newPassword=next.Text??""});current.Text=next.Text="";await page.DisplayAlertAsync("Senha alterada","Entre novamente com a nova senha.","Entendi");await CloudSessionStore.End(page,api);}),primary:true)));return page;
 }
}
