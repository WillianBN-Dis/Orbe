using Agenda.Core;
using static Agenda.App.OrbeUi;

namespace Agenda.App;

public sealed partial class DashboardPage
{
 private sealed record RecurringClient(string FullName);
 private sealed record RecurringResult(int Count,int Pending);

 private Task OpenRecurring()
 {
  if(repository?.EmployeeId is not {} employee)return NeedConnection();
  var seriesId=Guid.NewGuid();
  var title=Input("Nome do atendimento ou compromisso");
  var notes=new Editor{Placeholder="Descrição opcional",AutoSize=EditorAutoSizeOption.TextChanges,MinimumHeightRequest=80};
  var kinds=Enum.GetNames<EventKind>();
  var kind=new Picker{ItemsSource=kinds,SelectedIndex=0};
  var date=new DatePicker{Date=Selected.ToDateTime(TimeOnly.MinValue),Format="dd/MM/yyyy"};
  var start=new TimePicker{Time=TimeSpan.FromHours(9),Format="HH:mm"};
  var end=new TimePicker{Time=TimeSpan.FromHours(10),Format="HH:mm"};
  var frequency=Input("Ex.: 15","7");
  frequency.Keyboard=Keyboard.Numeric;
  var notifyUnavailable=new CheckBox{IsChecked=true};
  var counts=Enumerable.Range(2,51).ToArray();
  var count=new Picker{ItemsSource=counts,SelectedIndex=2};
  var amount=Input("0,00","0,00");
  amount.Keyboard=Keyboard.Numeric;
  var username=Input("Ex.: maria.silva");
  var result=Text("",14,Muted);
  string? confirmedUsername=null;
  username.TextChanged+=(_,_)=>{confirmedUsername=null;result.Text="";};
  var clientFields=Stack(10,Field("Usuário da cliente · opcional",username,"A cliente define o nome de usuário em Minha conta no site."),
   Button("Localizar cliente",async()=>{
    var candidate=username.Text?.Trim();
    if(string.IsNullOrWhiteSpace(candidate)){result.Text="Informe o nome de usuário da cliente.";return;}
    try{
     var found=await client.Get<RecurringClient>($"team/recurring/client?employeeId={Uri.EscapeDataString(employee)}&username={Uri.EscapeDataString(candidate!)}");
     if(username.Text?.Trim()!=candidate)return;
     confirmedUsername=candidate;result.Text="Cliente selecionada: "+found.FullName;
    }catch(Exception e){result.Text=FriendlyError(e);}
   }),result,Field("Valor previsto · R$",amount),
   Stack(4,Row(8,notifyUnavailable,Text("Avisar se precisar remarcar",14,Ink)),
    Text("Horários fora da jornada ou ocupados ficam pendentes. A cliente recebe a confirmação depois da remarcação.",12,Muted)));
  kind.SelectedIndexChanged+=(_,_)=>clientFields.IsVisible=kind.SelectedIndex==0;
  return OpenForm("Agendamento fixo","Repita no mesmo horário, a cada intervalo de dias.","calendar",
   [Field("Nome",title),Field("Tipo",kind),Field("Primeira data",date),Field("Horário de início",start),
    Field("Horário de término",end),Field("Repetir a cada quantos dias?",frequency),Field("Número de ocorrências",count),
    clientFields,Field("Descrição",notes)],async()=>{
    var service=kind.SelectedIndex==0;
    if(service&&!string.IsNullOrWhiteSpace(username.Text)&&confirmedUsername is null)
     throw new ArgumentException("Localize e confirme a cliente antes de salvar.");
    if(!int.TryParse(frequency.Text,out var days)||days is <1 or >365)
     throw new ArgumentException("Informe um intervalo de 1 a 365 dias.");
    var saved=await client.Post<RecurringResult>("team/recurring",new{
     seriesId,employeeId=employee,title=title.Text??"",kind=kinds[kind.SelectedIndex],clientUsername=service?confirmedUsername:null,
     notes=notes.Text,firstDate=DateOnly.FromDateTime(date.Date??DateTime.Today),
     startTime=TimeOnly.FromTimeSpan(start.Time??TimeSpan.Zero),endTime=TimeOnly.FromTimeSpan(end.Time??TimeSpan.Zero),
     everyDays=days,count=counts[count.SelectedIndex],notifyUnavailable=service&&notifyUnavailable.IsChecked,
     expectedCents=service?Finance.ParseCents(amount.Text??"0"):0L
    });
    if(saved.Pending>0)await DisplayAlertAsync("Série criada",$"{saved.Pending} ocorrência(s) aguardam remarcação na aba Notificações.","Entendi");
    return true;
   });
 }
}
