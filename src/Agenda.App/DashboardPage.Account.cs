using Agenda.Core;
using static Agenda.App.OrbeUi;

namespace Agenda.App;
public sealed partial class DashboardPage
{
 private void RenderAccount()
 {
  var current=repository?.EmployeeId;
  var scope=Stack(14,Text("Agenda em foco",19,Ink,true));
  if(profile.Owner&&repository is not null)
  {
   var choices=new[]{"Visão geral · todos os profissionais"}.Concat(people.Select(x=>x.Caption)).ToArray();var picker=new Picker{ItemsSource=choices,SelectedIndex=current is null?0:Array.FindIndex(people,x=>x.Id==current)+1};
   picker.SelectedIndexChanged+=async(_,_)=>await Guard(async()=>{var previous=repository.EmployeeId;repository.EmployeeId=picker.SelectedIndex<=0?null:people[picker.SelectedIndex-1].Id;try{await Reload();}catch{repository.EmployeeId=previous;throw;}Preferences.Default.Set("orbe-employee-id",repository.EmployeeId??"");BuildShell();});scope.Add(Field("Profissional",picker));
  }
  else scope.Add(Text(people.FirstOrDefault(x=>x.Id==profile.EmployeeId)?.DisplayName??profile.FullName,14,Muted));
  var personal=Stack(16,Text(profile.FullName,22,Ink,true),Text(profile.Email,13,Muted),Pill(profile.Owner?"ADMINISTRADOR":"PROFISSIONAL",Violet),Rule(),Button("Editar meu cadastro",async()=>await Navigation.PushAsync(AccountPages.Profile(client,profile))),Button("Jornadas, folgas e compromissos",async()=>await Navigation.PushAsync(AccountPages.Hours(client,profile,current))),Button("Abrir meu espaço no site",()=>Guard(async()=>await Launcher.Default.OpenAsync(new Uri(client.BaseUrl+"/equipe"))),quiet:true));
  var management=Stack(16,scope);
  if(profile.Owner)management.Add(Button("Configurar conexão",async()=>await Navigation.PushAsync(AccountPages.Connection(client))));
  if(profile.Owner)
  {
   management.Add(Rule());management.Add(Text("Seu negócio",20,Ink,true));
   var actions=new Grid{ColumnSpacing=16,RowSpacing=16};var columns=Wide?2:1;for(var c=0;c<columns;c++)actions.ColumnDefinitions.Add(new(GridLength.Star));
   var buttons=new[]{Button("Equipe e acessos",async()=>await Navigation.PushAsync(AccountPages.Employees(client))),Button("Serviços",async()=>await Navigation.PushAsync(AccountPages.Services(client))),Button("Regras e reservas online",async()=>await Navigation.PushAsync(AccountPages.Settings(client))),Button("Importar e atribuir histórico",async()=>await Navigation.PushAsync(AccountPages.Import(client)))};
   for(var i=0;i<buttons.Length;i++){if(i%columns==0)actions.RowDefinitions.Add(new(GridLength.Auto));buttons[i].HorizontalOptions=LayoutOptions.Fill;actions.Add(buttons[i],i%columns,i/columns);}management.Add(actions);
  }
  management.Add(Rule());management.Add(Button("Sair da conta",async()=>{if(await DisplayAlertAsync("Sair da sua conta?","Você precisará entrar novamente para acessar sua agenda.","Sair","Manter")){appointments=[];entries=[];await CloudSessionStore.End(this,client);}},danger:true));
#if ANDROID
  management.Add(Rule());management.Add(Button("Atualizar aplicativo",async()=>await Navigation.PushAsync(new UpdatePage())));
#endif
  var personalCard=Card(personal);personalCard.VerticalOptions=LayoutOptions.Start;var managementCard=Card(management);managementCard.VerticalOptions=LayoutOptions.Start;
  body.Add(Columns(Wide,personalCard,managementCard,1,1.5));
 }
}
