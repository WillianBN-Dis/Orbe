using Agenda.Core;
using static Agenda.App.OrbeUi;

namespace Agenda.App;

public sealed partial class DashboardPage
{
 private DateOnly? dayScrollDate;
 private double dayScrollOffset;
 private bool openingQuickAppointment;
 private async Task QuickAppointment(DateTime? start=null)
 {
  if(openingQuickAppointment)return;openingQuickAppointment=true;
  try{
   var destination=repository;
   if(destination?.EmployeeId is null){
    var available=people.Where(p=>p.Enabled).ToArray();
    if(available.Length==0){await NeedConnection();return;}
    var labels=available.Select((p,i)=>p.DisplayName+(available.Count(x=>x.DisplayName==p.DisplayName)>1?$" ({i+1})":"")).ToArray();
    var chosen=await DisplayActionSheetAsync("Qual profissional?","Voltar",null,labels);var index=Array.IndexOf(labels,chosen);if(index<0)return;
    destination=new CloudAgendaRepository(client,available[index].Id);
   }
   await OpenAppointment(suggestedStart:start??Selected.ToDateTime(new TimeOnly(9,0)),bookingRepository:destination);
  }finally{openingQuickAppointment=false;}
 }
 private sealed record DaySlot(Stored<Appointment> Item,double Start,double End,int Lane,int Lanes);
 private DaySlot[] DaySlots()
 {
  var midnight=Selected.ToDateTime(TimeOnly.MinValue);
  var timed=appointments.Where(a=>a.Value.Intersects(Selected)&&!CoversDay(a.Value))
   .Select(a=>new{Item=a,Start=Math.Max(0,(a.Value.Start-midnight).TotalMinutes),End=Math.Min(1440,(a.Value.End-midnight).TotalMinutes)})
   .OrderBy(a=>a.Start).ThenBy(a=>a.End).ToArray();
  var result=new List<DaySlot>();
  for(var index=0;index<timed.Length;){
   var end=index+1;var clusterEnd=timed[index].End;
   while(end<timed.Length&&timed[end].Start<clusterEnd){clusterEnd=Math.Max(clusterEnd,timed[end].End);end++;}
   var lanes=new List<double>();var cluster=new List<(int Index,int Lane)>();
   for(var i=index;i<end;i++){
    var lane=lanes.FindIndex(until=>until<=timed[i].Start);
    if(lane<0){lane=lanes.Count;lanes.Add(timed[i].End);}else lanes[lane]=timed[i].End;
    cluster.Add((i,lane));
   }
   result.AddRange(cluster.Select(c=>new DaySlot(timed[c.Index].Item,timed[c.Index].Start,timed[c.Index].End,c.Lane,lanes.Count)));
   index=end;
  }
  return result.ToArray();
 }
 private bool CoversDay(Appointment item)=>item.Start<=Selected.ToDateTime(TimeOnly.MinValue)&&item.End>=Selected.AddDays(1).ToDateTime(TimeOnly.MinValue);
 private View BuildDayGrid(View modes)
 {
  var midnight=Selected.ToDateTime(TimeOnly.MinValue);
  var top=Stack(14,Between(modes,Button("+ Agendar",()=>QuickAppointment(),primary:true)),Between(Text(TitleCase(Selected.ToString("dddd, dd 'de' MMMM",Finance.Brazilian)),17,Ink,true),
   Row(2,Button("‹",()=>SelectDay(Selected.AddDays(-1)),quiet:true),Button("Hoje",()=>SelectDay(DateOnly.FromDateTime(client.Local(DateTimeOffset.UtcNow))),quiet:true),Button("›",()=>SelectDay(Selected.AddDays(1)),quiet:true))));
  foreach(var birthday in BirthdayOn(Selected))top.Add(Pill("Aniversário · "+birthday.Name,BirthdayColor));
  foreach(var fullDay in appointments.Where(a=>CoversDay(a.Value)))
   top.Add(Button("Dia inteiro · "+fullDay.Value.Title,()=>AppointmentActions(fullDay),quiet:true));
  top.Add(Text("Toque em um horário livre para agendar ou em um evento para ver as opções",11,Muted));
  const double hourHeight=96;const double totalHeight=24*hourHeight+12;
  var hours=new AbsoluteLayout{HeightRequest=totalHeight,WidthRequest=48};
  var canvas=new AbsoluteLayout{HeightRequest=totalHeight,IsClippedToBounds=true};
  for(var halfHour=0;halfHour<48;halfHour++){
   var start=midnight.AddMinutes(halfHour*30);var hit=Button("",()=>QuickAppointment(start),quiet:true);
   hit.MinimumHeightRequest=0;hit.MaximumWidthRequest=-1;hit.Padding=0;hit.HorizontalOptions=LayoutOptions.Fill;
   SemanticProperties.SetDescription(hit,$"Agendar em {start:dd/MM/yyyy 'às' HH:mm}");canvas.Add(hit);
   AbsoluteLayout.SetLayoutBounds(hit,new Rect(0,halfHour*hourHeight/2+10,1,hourHeight/2));AbsoluteLayout.SetLayoutFlags(hit,Microsoft.Maui.Layouts.AbsoluteLayoutFlags.WidthProportional);
  }
  for(var hour=0;hour<24;hour++){
   var label=Text($"{hour:D2}:00",10,Dim);hours.Add(label);AbsoluteLayout.SetLayoutBounds(label,new Rect(0,hour*hourHeight,48,20));
   var line=new BoxView{Color=Line,InputTransparent=true};canvas.Add(line);
   AbsoluteLayout.SetLayoutBounds(line,new Rect(0,hour*hourHeight+10,1,1));AbsoluteLayout.SetLayoutFlags(line,Microsoft.Maui.Layouts.AbsoluteLayoutFlags.WidthProportional);
   var half=new BoxView{Color=Line.WithAlpha(.35f),InputTransparent=true};canvas.Add(half);
   AbsoluteLayout.SetLayoutBounds(half,new Rect(0,hour*hourHeight+hourHeight/2+10,1,1));AbsoluteLayout.SetLayoutFlags(half,Microsoft.Maui.Layouts.AbsoluteLayoutFlags.WidthProportional);
  }
  var slots=DaySlots();var cards=new List<(Border Card,DaySlot Slot)>();
  foreach(var slot in slots){
   var item=slot.Item.Value;var tint=KindColor(item.Kind);var height=Math.Max(18,(slot.End-slot.Start)*hourHeight/60-3);
   var title=Text(item.Title,compact?11:13,Ink,true);title.MaxLines=1;title.LineBreakMode=LineBreakMode.TailTruncation;
   var content=Stack(2,title);
   if(height>=36)content.Add(Text($"{item.Start:HH:mm}–{item.End:HH:mm}",10,tint));
   if(height>=64&&!string.IsNullOrWhiteSpace(item.Client)){var person=Text(item.Client,11,Muted);person.MaxLines=1;person.LineBreakMode=LineBreakMode.TailTruncation;content.Add(person);}
   content.IsClippedToBounds=true;var card=Card(content,2,tint.WithAlpha(.16f),tint.WithAlpha(.6f),7);card.Padding=new Thickness(6,2);
   var tap=new TapGestureRecognizer();tap.Tapped+=async(_,_)=>await AppointmentActions(slot.Item);card.GestureRecognizers.Add(tap);
   SemanticProperties.SetDescription(card,$"{item.Kind}: {item.Title}, de {item.Start:HH:mm} até {item.End:HH:mm}");
   canvas.Add(card);cards.Add((card,slot));
  }
  void Arrange(){if(canvas.Width<=0)return;foreach(var (card,slot) in cards){var width=canvas.Width/slot.Lanes;AbsoluteLayout.SetLayoutBounds(card,new Rect(slot.Lane*width+2,slot.Start*hourHeight/60+10,Math.Max(1,width-4),Math.Max(18,(slot.End-slot.Start)*hourHeight/60-3)));}}
  canvas.SizeChanged+=(_,_)=>Arrange();
  var now=client.Local(DateTimeOffset.UtcNow);
  if(DateOnly.FromDateTime(now)==Selected){
   var current=new BoxView{Color=Coral,HeightRequest=2,InputTransparent=true};canvas.Add(current);
   AbsoluteLayout.SetLayoutBounds(current,new Rect(0,now.TimeOfDay.TotalMinutes*hourHeight/60+10,1,2));AbsoluteLayout.SetLayoutFlags(current,Microsoft.Maui.Layouts.AbsoluteLayoutFlags.WidthProportional);
  }
  var grid=new Grid{ColumnDefinitions=new(){new(new GridLength(48)),new(GridLength.Star)},ColumnSpacing=4,HeightRequest=totalHeight};grid.Add(hours);grid.Add(canvas,1);
  var scroll=new DayTimelineScrollView{Content=grid,HeightRequest=compact?560:640,VerticalScrollBarVisibility=ScrollBarVisibility.Always};
  var renderedDay=Selected;
  scroll.Scrolled+=(_,e)=>{dayScrollDate=renderedDay;dayScrollOffset=e.ScrollY;};
  scroll.Loaded+=async(_,_)=>{if(Selected!=renderedDay)return;var minute=slots.Length==0?7*60:Math.Max(0,slots.Min(s=>s.Start)-60);var target=dayScrollDate==Selected?dayScrollOffset:minute*hourHeight/60;await scroll.ScrollToAsync(0,Math.Min(target,totalHeight-scroll.Height),false);};
  top.Add(scroll);return Card(top,compact?12:20);
 }
}
