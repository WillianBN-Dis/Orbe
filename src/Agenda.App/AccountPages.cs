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
 private static readonly string[] Days=["Domingo","Segunda-feira","Terça-feira","Quarta-feira","Quinta-feira","Sexta-feira","Sábado"];
 private sealed record PeriodRow(Picker? Day,Entry Start,Entry End,View View);
 private sealed record DayRow(DatePicker Date,List<PeriodRow> Periods,View View);
 private static string Clock(int minute)=>$"{minute/60:00}:{minute%60:00}";
 private static int Minute(Entry input,bool end=false)
 {if(end&&input.Text=="24:00")return 1440;if(!TimeOnly.TryParseExact(input.Text,"HH:mm",out var value))throw new ArgumentException("Use horários no formato HH:mm, por exemplo 09:00.");return value.Hour*60+value.Minute;}
 private static void AddPeriod(VerticalStackLayout host,List<PeriodRow> rows,bool weekly,int day=1,int start=540,int end=1080)
 {
  var dayPicker=weekly?new Picker{ItemsSource=Days,SelectedIndex=day}:null;
  Entry from=Input("09:00",Clock(start)),until=Input("18:00",Clock(end));
  var content=Stack(12);if(dayPicker is not null)content.Add(Field("Dia da semana",dayPicker));content.Add(Columns(false,Field("Início · HH:mm",from),Field("Fim · HH:mm",until,"Use 24:00 para terminar à meia-noite.")));
  var view=Card(content,16,CanvasColor);var item=new PeriodRow(dayPicker,from,until,view);rows.Add(item);content.Add(Button("Remover período",()=>{rows.Remove(item);host.Children.Remove(view);return Task.CompletedTask;},quiet:true));host.Add(view);
 }
 internal static Page Hours(CloudApi api,CloudProfile profile,string? selected)
 {
  var page=new CloudFormPage(api,"Minha disponibilidade");
  page.Load=async()=>
  {
   var people=profile.Owner?await api.Get<CloudEmployee[]>("team/employees"):await Active(api);page.Fields.Clear();if(people.Length==0){page.Fields.Add(Text("Cadastre um profissional ativo na seção Equipe para configurar a jornada.",14,Muted));return;}
   var picker=People(people,selected??profile.EmployeeId??people.FirstOrDefault(x=>x.Enabled)?.Id??people[0].Id);var host=Stack(16);page.Fields.Add(Field("Profissional",picker));page.Fields.Add(host);
   async Task Render()
   {
    var id=Picked(picker);var data=await api.Get<JsonElement>("team/hours?employeeId="+Uri.EscapeDataString(id));host.Children.Clear();var weekly=Stack(12);var rows=new List<PeriodRow>();var overrides=Stack(14);var days=new List<DayRow>();
    host.Add(Text("Jornada semanal",23,Ink,true));host.Add(Text("Adicione vários períodos para separar almoço ou outros intervalos. Suas alterações não precisam de aprovação.",13,Muted));host.Add(weekly);
    foreach(var row in data.GetProperty("weekly").EnumerateArray())AddPeriod(weekly,rows,true,I(row,"day"),I(row.GetProperty("window"),"startMinute"),I(row.GetProperty("window"),"endMinute"));
    host.Add(Button("+ Período de trabalho",()=>{AddPeriod(weekly,rows,true);return Task.CompletedTask;}));host.Add(Rule());host.Add(Text("Datas especiais",21,Ink,true));host.Add(Text("A data especial substitui a jornada semanal. Sem períodos, o dia inteiro fica indisponível.",12,Muted));host.Add(overrides);
    void AddDay(DateOnly? date=null,JsonElement? windows=null)
    {
     var control=new DatePicker{Date=(date??DateOnly.FromDateTime(api.Local(DateTimeOffset.UtcNow))).ToDateTime(TimeOnly.MinValue),Format="dd/MM/yyyy"};var periods=Stack(12);var list=new List<PeriodRow>();var inner=Stack(14,Field("Data especial",control),periods);var card=Card(inner,16);var entry=new DayRow(control,list,card);days.Add(entry);overrides.Add(card);
     inner.Add(Button("+ Período nesta data",()=>{AddPeriod(periods,list,false);return Task.CompletedTask;}));inner.Add(Button("Remover data especial",()=>{days.Remove(entry);overrides.Children.Remove(card);return Task.CompletedTask;},danger:true));
     if(windows is {ValueKind:JsonValueKind.Array})foreach(var w in windows.Value.EnumerateArray())AddPeriod(periods,list,false,0,I(w,"startMinute"),I(w,"endMinute"));
    }
    foreach(var day in data.GetProperty("exceptions").EnumerateArray())AddDay(DateOnly.Parse(S(day,"date")),day.GetProperty("windows"));
    host.Add(Button("+ Data especial",()=>{AddDay();return Task.CompletedTask;}));host.Add(Button("Salvar disponibilidade",()=>page.Run(async()=>{await api.Put<JsonElement>("team/hours?employeeId="+Uri.EscapeDataString(id),new{version=S(data,"version"),weekly=rows.Select(x=>new{day=x.Day!.SelectedIndex,window=new{startMinute=Minute(x.Start),endMinute=Minute(x.End,true)}}).ToArray(),exceptions=days.Select(x=>new{date=DateOnly.FromDateTime(x.Date.Date??DateTime.Today),windows=x.Periods.Select(w=>new{startMinute=Minute(w.Start),endMinute=Minute(w.End,true)}).ToArray()}).ToArray()});await page.DisplayAlertAsync("Disponibilidade atualizada","Os novos horários já serão usados no site.","Entendi");await Render();}),primary:true));host.Add(Button("Adicionar folga ou compromisso",async()=>await page.Navigation.PushAsync(Block(api,id))));
   }
   picker.SelectedIndexChanged+=async(_,_)=>await page.Run(Render);await Render();
  };return page;
 }
 internal static Page Block(CloudApi api,string employee)
 {
  var page=new CloudFormPage(api,"Folga ou compromisso");var title=Input("Ex.: Folga");var notes=new Editor{Placeholder="Observações privadas",AutoSize=EditorAutoSizeOption.TextChanges,MinimumHeightRequest=90};var kind=new Picker{ItemsSource=new[]{"Folga","Compromisso","Outro"},SelectedIndex=0};
  var today=api.Local(DateTimeOffset.UtcNow).Date;DatePicker from=new DatePicker{Date=today,Format="dd/MM/yyyy"},until=new DatePicker{Date=today,Format="dd/MM/yyyy"};TimePicker start=new TimePicker{Time=TimeSpan.FromHours(9),Format="HH:mm"},end=new TimePicker{Time=TimeSpan.FromHours(10),Format="HH:mm"};var all=new Switch{IsToggled=true};var times=Stack(14,Field("Início",start),Field("Data de término",until),Field("Fim",end));times.IsVisible=false;all.Toggled+=(_,e)=>times.IsVisible=!e.Value;
  page.Fields.Add(Stack(18,Text("Reserve seu tempo",24,Ink,true),Field("Título",title),Field("Tipo",kind),Field("Dia",from),Between(Text("Dia inteiro",13,Muted,true),all),times,Field("Observações",notes),Text("Reservas existentes não são canceladas. Se houver conflito, você precisará resolvê-lo antes.",12,Muted),Button("Salvar bloqueio",()=>page.Run(async()=>{var day=(from.Date??today).Date;var begins=all.IsToggled?day:day+(start.Time??TimeSpan.Zero);var ends=all.IsToggled?day.AddDays(1):(until.Date??day).Date+(end.Time??TimeSpan.Zero);await api.Post<JsonElement>("team/blocks",new{employeeId=employee,title=Required(title,"o título"),kind=kind.SelectedItem,start=api.Instant(begins),end=api.Instant(ends),notes=notes.Text});await Back(page);}),primary:true)));return page;
 }
}
