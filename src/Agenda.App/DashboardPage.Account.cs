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
   var choices=new[]{"Visão geral · todos os profissionais"}.Concat(people.Select(x=>x.DisplayName)).ToArray();var picker=new Picker{ItemsSource=choices,SelectedIndex=current is null?0:Array.FindIndex(people,x=>x.Id==current)+1};
   picker.SelectedIndexChanged+=async(_,_)=>await Guard(async()=>{var previous=repository.EmployeeId;repository.EmployeeId=picker.SelectedIndex<=0?null:people[picker.SelectedIndex-1].Id;try{await Reload();}catch{repository.EmployeeId=previous;throw;}Preferences.Default.Set("orbe-employee-id",repository.EmployeeId??"");BuildShell();});scope.Add(Field("Profissional",picker));
  }
  else scope.Add(Text(people.FirstOrDefault(x=>x.Id==profile.EmployeeId)?.DisplayName??profile.FullName,14,Muted));
  var personal=Stack(16,Text(profile.FullName,22,Ink,true),Text(profile.Email,13,Muted),Pill(profile.Owner?"ADMINISTRADOR":"PROFISSIONAL",Violet),Rule(),Button("Editar meu cadastro",async()=>await Navigation.PushAsync(AccountPages.Profile(client,profile))),Button("Jornadas, folgas e compromissos",async()=>await Navigation.PushAsync(AccountPages.Hours(client,profile,current))),Button("Abrir meu espaço no site",()=>Guard(async()=>await Launcher.Default.OpenAsync(new Uri(client.BaseUrl+"/equipe"))),quiet:true));
  var management=Stack(16,scope);
  if(profile.Owner){management.Add(Rule());management.Add(Text("Seu negócio",20,Ink,true));management.Add(Button("Equipe e acessos",async()=>await Navigation.PushAsync(AccountPages.Employees(client))));management.Add(Button("Serviços",async()=>await Navigation.PushAsync(AccountPages.Services(client))));management.Add(Button("Regras e reservas online",async()=>await Navigation.PushAsync(AccountPages.Settings(client))));management.Add(Button("Importar e atribuir histórico",async()=>await Navigation.PushAsync(AccountPages.Import(client))));}
  management.Add(Rule());management.Add(Button("Sair da conta",async()=>{if(await DisplayAlertAsync("Sair da sua conta?","Você precisará entrar novamente para acessar sua agenda.","Sair","Manter")){appointments=[];entries=[];await CloudSessionStore.End(this,client);}},danger:true));
  body.Add(Columns(Wide,Card(personal),Card(management)));
 }
}
