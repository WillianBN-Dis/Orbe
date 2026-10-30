using System.Text.Json;
using Agenda.Core;
using static Agenda.App.OrbeUi;

namespace Agenda.App;
internal static partial class AccountPages
{
 private static readonly string[] Days=["Domingo","Segunda-feira","Terça-feira","Quarta-feira","Quinta-feira","Sexta-feira","Sábado"];
 private sealed record TimePeriod(TimePicker Start,TimePicker End,CheckBox Disabled,CheckBox Midnight)
 {
  internal object Window()=>new{startMinute=(int)(Start.Time??TimeSpan.Zero).TotalMinutes,endMinute=Midnight.IsChecked?1440:(int)(End.Time??TimeSpan.Zero).TotalMinutes};
 }
 private sealed record ScheduleDay(Picker? Weekday,DatePicker? Date,List<TimePeriod> Periods);
 private static TimePeriod AddTimePeriod(VerticalStackLayout host,string label,int start,int end,bool disabled=false)
 {
  var from=new TimePicker{Time=TimeSpan.FromMinutes(start),Format="HH:mm"};var until=new TimePicker{Time=TimeSpan.FromMinutes(end==1440?0:end),Format="HH:mm"};
  var off=new CheckBox{IsChecked=disabled,Color=Violet};var midnight=new CheckBox{IsChecked=end==1440,Color=Violet};
  var fields=Columns(true,Field("Início",from),Field("Fim",until));
  void Update(){from.IsEnabled=until.IsEnabled=midnight.IsEnabled=!off.IsChecked;until.IsEnabled&=!midnight.IsChecked;fields.Opacity=off.IsChecked?.4:1;}
  off.CheckedChanged+=(_,_)=>Update();midnight.CheckedChanged+=(_,_)=>Update();
  host.Add(Card(Stack(10,Between(Text(label,16,Ink,true),Row(6,off,Text("Desativado",12,Muted))),fields,Row(6,midnight,Text("Termina à meia-noite",11,Muted))),14,CanvasColor));Update();return new(from,until,off,midnight);
 }
 internal static Page Hours(CloudApi api,CloudProfile profile,string? selected)
 {
  var page=new CloudFormPage(api,"Minha disponibilidade");page.Load=async()=>{
   var people=profile.Owner?await api.Get<CloudEmployee[]>("team/employees"):await Active(api);page.Fields.Clear();
   if(people.Length==0){page.Fields.Add(Text("Cadastre um profissional ativo na seção Equipe para configurar a jornada.",14,Muted));return;}
   var picker=People(people,selected??profile.EmployeeId??people[0].Id);page.Fields.Add(Field("Profissional",picker));var host=Stack(16);page.Fields.Add(host);
   async Task Render(){
    var id=Picked(picker);var data=await api.Get<JsonElement>("team/hours?employeeId="+Uri.EscapeDataString(id));host.Children.Clear();var weekly=Stack(14);var exceptions=Stack(14);var weekRows=new List<ScheduleDay>();var dateRows=new List<ScheduleDay>();
    void AddDay(bool special,int weekday=1,DateOnly? date=null,JsonElement[]? windows=null){
     var content=Stack(14);var periods=Stack(12);var day=special?null:new Picker{ItemsSource=Days,SelectedIndex=weekday};var calendar=special?new DatePicker{Date=(date??DateOnly.FromDateTime(api.Local(DateTimeOffset.UtcNow))).ToDateTime(TimeOnly.MinValue),Format="dd/MM/yyyy"}:null;
     content.Add(special?Field("Data especial",calendar!):Field("Dia da semana",day!));content.Add(periods);var rows=new List<TimePeriod>();
     if(windows is null){rows.Add(AddTimePeriod(periods,"Manhã",540,720));rows.Add(AddTimePeriod(periods,"Tarde",780,1080));}
     else if(windows.Length==0){rows.Add(AddTimePeriod(periods,"Manhã",540,720,true));rows.Add(AddTimePeriod(periods,"Tarde",780,1080,true));}
     else for(var n=0;n<windows.Length;n++){var w=windows[n];rows.Add(AddTimePeriod(periods,"Período "+(n+1),I(w,"startMinute"),I(w,"endMinute")));}
     var record=new ScheduleDay(day,calendar,rows);var collection=special?dateRows:weekRows;var target=special?exceptions:weekly;collection.Add(record);var card=Card(content,16);target.Add(card);
     content.Add(Button("+ Outro período",()=>{rows.Add(AddTimePeriod(periods,"Período extra",1080,1200));return Task.CompletedTask;},quiet:true));
     content.Add(Button("Remover "+(special?"data":"jornada"),()=>{collection.Remove(record);target.Children.Remove(card);return Task.CompletedTask;},danger:true));
    }
    host.Add(Text("Jornada semanal",23,Ink,true));host.Add(Text("Escolha o dia e os horários. Marque Desativado no período em que não atende.",13,Muted));host.Add(weekly);
    foreach(var group in data.GetProperty("weekly").EnumerateArray().GroupBy(x=>I(x,"day")))AddDay(false,group.Key,windows:group.Select(x=>x.GetProperty("window").Clone()).ToArray());
    host.Add(Button("+ Jornada · manhã e tarde",()=>{AddDay(false);return Task.CompletedTask;}));host.Add(Rule());host.Add(Text("Datas especiais",21,Ink,true));host.Add(Text("Substituem a jornada semanal. Desative todos os períodos para deixar o dia inteiro indisponível.",12,Muted));host.Add(exceptions);
    foreach(var item in data.GetProperty("exceptions").EnumerateArray())AddDay(true,date:DateOnly.Parse(S(item,"date")),windows:item.GetProperty("windows").EnumerateArray().Select(x=>x.Clone()).ToArray());
    host.Add(Button("+ Data especial",()=>{AddDay(true);return Task.CompletedTask;}));
    host.Add(Button("Salvar disponibilidade",()=>page.Run(async()=>{
     await api.Put<JsonElement>("team/hours?employeeId="+Uri.EscapeDataString(id),new{version=S(data,"version"),weekly=weekRows.SelectMany(d=>d.Periods.Where(p=>!p.Disabled.IsChecked).Select(p=>new{day=d.Weekday!.SelectedIndex,window=p.Window()})).ToArray(),exceptions=dateRows.Select(d=>new{date=DateOnly.FromDateTime(d.Date!.Date??DateTime.Today),windows=d.Periods.Where(p=>!p.Disabled.IsChecked).Select(p=>p.Window()).ToArray()}).ToArray()});await page.DisplayAlertAsync("Disponibilidade atualizada","Os novos horários já aparecem no site.","Entendi");await Render();
    }),primary:true));host.Add(Button("Adicionar folga ou compromisso",async()=>await page.Navigation.PushAsync(Block(api,id))));
   }
   picker.SelectedIndexChanged+=async(_,_)=>await page.Run(Render);await Render();
  };return page;
 }
 internal static Page Block(CloudApi api,string employee)
 {
  var page=new CloudFormPage(api,"Folga ou compromisso");var title=Input("Ex.: Folga");var notes=new Editor{Placeholder="Observações privadas",MinimumHeightRequest=90};var kind=new Picker{ItemsSource=new[]{"Folga","Compromisso","Outro"},SelectedIndex=0};var today=api.Local(DateTimeOffset.UtcNow).Date;
  var date=new DatePicker{Date=today,Format="dd/MM/yyyy"};var scope=new Picker{ItemsSource=new[]{"Dia inteiro","Manhã","Tarde","Escolher horários"},SelectedIndex=0};var from=new TimePicker{Time=TimeSpan.FromHours(9),Format="HH:mm"};var until=new TimePicker{Time=TimeSpan.FromHours(12),Format="HH:mm"};var times=Columns(true,Field("Início",from),Field("Fim",until));times.IsVisible=false;
  scope.SelectedIndexChanged+=(_,_)=>{times.IsVisible=scope.SelectedIndex!=0;if(scope.SelectedIndex==1){from.Time=TimeSpan.FromHours(9);until.Time=TimeSpan.FromHours(12);}if(scope.SelectedIndex==2){from.Time=TimeSpan.FromHours(13);until.Time=TimeSpan.FromHours(18);}};
  page.Fields.Add(Stack(18,Text("Reserve seu tempo",24,Ink,true),Field("Título",title),Field("Tipo",kind),Field("Dia",date),Field("Período",scope),times,Field("Observações",notes),Text("Os horários de manhã e tarde são sugestões; ajuste se necessário. Conflitos com atendimentos confirmados serão recusados.",12,Muted),Button("Salvar bloqueio",()=>page.Run(async()=>{var day=(date.Date??today).Date;var all=scope.SelectedIndex==0;await api.Post<JsonElement>("team/blocks",new{employeeId=employee,title=Required(title,"o título"),kind=kind.SelectedItem,start=api.Instant(all?day:day+(from.Time??TimeSpan.Zero)),end=api.Instant(all?day.AddDays(1):day+(until.Time??TimeSpan.Zero)),notes=notes.Text});await Back(page);}),primary:true)));return page;
 }
}
