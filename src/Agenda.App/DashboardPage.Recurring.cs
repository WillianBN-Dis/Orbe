using Agenda.Core;
using static Agenda.App.OrbeUi;

namespace Agenda.App;

public sealed partial class DashboardPage
{
 private sealed record RecurringClient(string FullName);
 private sealed record RecurringResult(int Count);

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
  var intervals=new[]{1,2,3,4,6,8,12};
  var frequency=new Picker{ItemsSource=intervals.Select(x=>x==1?"Toda semana":$"A cada {x} semanas").ToArray(),SelectedIndex=0};
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
   }),result,Field("Valor previsto · R$",amount));
  kind.SelectedIndexChanged+=(_,_)=>clientFields.IsVisible=kind.SelectedIndex==0;
  return OpenForm("Agendamento fixo","Crie uma série no mesmo dia da semana e horário.","calendar",
   [Field("Nome",title),Field("Tipo",kind),Field("Primeira data",date),Field("Horário de início",start),
    Field("Horário de término",end),Field("Frequência",frequency),Field("Número de ocorrências",count),
    clientFields,Field("Descrição",notes)],async()=>{
    var service=kind.SelectedIndex==0;
    if(service&&!string.IsNullOrWhiteSpace(username.Text)&&confirmedUsername is null)
     throw new ArgumentException("Localize e confirme a cliente antes de salvar.");
    await client.Post<RecurringResult>("team/recurring",new{
     seriesId,employeeId=employee,title=title.Text??"",kind=kinds[kind.SelectedIndex],clientUsername=service?confirmedUsername:null,
     notes=notes.Text,firstDate=DateOnly.FromDateTime(date.Date??DateTime.Today),
     startTime=TimeOnly.FromTimeSpan(start.Time??TimeSpan.Zero),endTime=TimeOnly.FromTimeSpan(end.Time??TimeSpan.Zero),
     everyWeeks=intervals[frequency.SelectedIndex],count=counts[count.SelectedIndex],
     expectedCents=service?Finance.ParseCents(amount.Text??"0"):0L
    });
    return true;
   });
 }
}
