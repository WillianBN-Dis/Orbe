#if DEBUG
using System.Diagnostics;
using Agenda.Core;
namespace Agenda.App;
public sealed partial class DashboardPage
{
 private static IEnumerable<T> FindViews<T>(IVisualTreeElement root)where T:class
 {if(root is T value)yield return value;foreach(var child in root.GetVisualChildren())foreach(var match in FindViews<T>(child))yield return match;}
 internal void SeedCalendarMarkers()
 {
  var start=calendarMonth.AddDays(1).ToDateTime(new TimeOnly(10,0));
  birthdays=[new("birthday-qa","Cliente aniversário QA",start.Month,start.Day)];
  entries=[new(new("receipt-qa","Recebimento QA",EntryKind.Receita,DateOnly.FromDateTime(start),5000,"qa-0",true),new([],[]))];
  appointments=Enum.GetValues<EventKind>().Append(EventKind.Atendimento)
   .Select((kind,index)=>new Stored<Appointment>(new($"qa-{index}","Evento sintético",kind,start,start.AddHours(1),null,null,0),new([],[])))
   .ToArray();
 }

 internal async Task QaInteractions()
 {
  var initial=calendarButtons.Values.ToArray();var nav=navigationButtons.Keys.ToArray();var month=calendarMonth;var watch=Stopwatch.StartNew();
#if WINDOWS
  var dayButton=initial.First();
  var cursorProperty=typeof(Microsoft.UI.Xaml.UIElement).GetProperty("ProtectedCursor",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
  var native=(Microsoft.UI.Xaml.Controls.Control)dayButton.Handler!.PlatformView!;
  var enabledCursor=cursorProperty.GetValue(native);
  if(enabledCursor is not Microsoft.UI.Input.InputSystemCursor)throw new Exception("Clickable calendar must use hand cursor");
  dayButton.IsEnabled=false;for(var attempt=0;attempt<20&&ReferenceEquals(enabledCursor,cursorProperty.GetValue(native));attempt++)await Task.Delay(50);
  if(cursorProperty.GetValue(native) is not Microsoft.UI.Input.InputSystemCursor || ReferenceEquals(enabledCursor,cursorProperty.GetValue(native)))throw new Exception("Disabled calendar must use arrow cursor");
  dayButton.IsEnabled=true;
  if(!compact&&(dayButton.WidthRequest<56||dayButton.HeightRequest<56))throw new Exception("Desktop calendar targets too small");
#endif
  await SelectDay(month.AddDays(1));var milliseconds=watch.Elapsed.TotalMilliseconds;
  var dayCell=(Grid)calendarButtons[month.AddDays(1)].Parent;
  var markers=dayCell.Children.OfType<HorizontalStackLayout>().Single();
  var colors=markers.Children.OfType<Microsoft.Maui.Controls.Shapes.Ellipse>()
   .Select(dot=>((SolidColorBrush)dot.Fill).Color).ToArray();
  if(colors.Length!=5||colors.Distinct().Count()!=5||!markers.InputTransparent)
   throw new Exception("Expected one visible marker per event type, with the day still clickable");
  if(!FindViews<Label>(agendaTimeline).Any(l=>l.Text=="Cliente aniversário QA"))throw new Exception("Birthday missing from day timeline");
  if(!FindViews<Label>(agendaTimeline).Any(l=>l.Text=="Recebimento registrado"))throw new Exception("Receipt state missing");
  if(!initial.SequenceEqual(calendarButtons.Values))throw new Exception("Calendar controls rebuilt on selection");
  if(selected!=month.AddDays(1))throw new Exception("Selected date not updated");
  await MoveMonth(1);await SelectDay(DateOnly.FromDateTime(DateTime.Today));
  if(calendarMonth.Month!=selected.Month||calendarMonth.Year!=selected.Year)throw new Exception("Today did not restore month");
  await SwitchTab("Conta");await SwitchTab("Financeiro");await SwitchTab("Agenda");
  if(!nav.SequenceEqual(navigationButtons.Keys))throw new Exception("Navigation controls rebuilt on tab change");
  var weekdayLabels=FindViews<Label>(agendaCalendar).Where(l=>new[]{"DOM","SEG","TER","QUA","QUI","SEX","SÁB"}.Contains(l.Text)).Select(l=>l.Text).ToArray();
  if(!weekdayLabels.SequenceEqual(new[]{"DOM","SEG","TER","QUA","QUI","SEX","SÁB"}))throw new Exception("Sunday-first calendar missing");
#if WINDOWS
  await SelectDay(month.AddDays(1));
  async Task Capture(string mode){
   await Task.Delay(300);
   if(Window?.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow&&nativeWindow.Content is Microsoft.UI.Xaml.FrameworkElement root)
    await StartupCapture.Capture(root,System.IO.Path.Combine(System.IO.Path.GetTempPath(),$"orbe-calendar-{mode}-{Environment.ProcessId}.png"));
  }
  await Capture("month");
  await mainScroll.ScrollToAsync(agendaTimeline,ScrollToPosition.Start,false);await Capture("birthday");
  await mainScroll.ScrollToAsync(0,0,false);
#endif
  agendaView="Ano";agendaCalendar.Content=BuildCalendar();
  if(FindViews<Button>(agendaCalendar).Count(b=>System.Globalization.CultureInfo.GetCultureInfo("pt-BR").DateTimeFormat.MonthNames.Contains(b.Text,StringComparer.OrdinalIgnoreCase))!=12)
   throw new Exception("Expected 12 months in year view");
#if WINDOWS
  await Capture("year");
#endif
  agendaView="Dia";await SelectDay(month.AddDays(1));agendaCalendar.Content=BuildCalendar();
  var daySlots=DaySlots();if(daySlots.Length!=5||daySlots.Any(s=>s.Lanes!=5)||daySlots.Select(s=>s.Lane).Distinct().Count()!=5)throw new Exception("Daily overlap columns failed");
  var begin=Selected.ToDateTime(new TimeOnly(9,0));
  appointments=[new(new("qa-day-cut","Corte feminino",EventKind.Atendimento,begin,begin.AddMinutes(90),"Cliente sintética",null,8000),new([],[])),new(new("qa-day-task","Compromisso",EventKind.Compromisso,begin.AddMinutes(90),begin.AddHours(2),null,null,0),new([],[])),new(new("qa-day-flex","Coloração",EventKind.Atendimento,begin.AddMinutes(135),begin.AddHours(4),"Cliente sintética",null,12000,true),new([],[])),new(new("qa-day-all","Evento de dia inteiro",EventKind.Outro,Selected.ToDateTime(TimeOnly.MinValue),Selected.AddDays(1).ToDateTime(TimeOnly.MinValue),null,null,0),new([],[]))];
  if(DaySlots().Length!=3||DaySlots().Any(slot=>slot.Lanes!=1)||Math.Abs(DaySlots()[0].End-DaySlots()[0].Start-90)>0.01)throw new Exception("Duration and all-day layout failed");
  dayScrollDate=null;UpdateAgendaDay();agendaCalendar.Content=BuildCalendar();
  await mainScroll.ScrollToAsync(agendaCalendar,ScrollToPosition.Start,false);
  if(FindViews<Label>(agendaCalendar).Count(l=>l.Text is {} label&&label.Length==5&&label.EndsWith(":00"))!=24)throw new Exception("Daily hours missing");
#if WINDOWS
  await Capture("day");
#endif
  var nextDay=Selected.AddDays(1);await SelectDay(nextDay);
  if(selected!=nextDay||calendarButtons.Count!=0)throw new Exception("Day view navigation failed");
#if WINDOWS
  await Capture("day-empty");
#endif
  var scopeId="quick-professional-qa";people=[new(scopeId,"Profissional QA",true,Guid.NewGuid())];repository=new(client,scopeId);
  var requested=Selected.ToDateTime(new TimeOnly(14,30));await QuickAppointment(requested);await Task.Delay(250);
  var editor=Navigation.NavigationStack.Last();
  var dateFields=FindViews<DatePicker>(editor).ToArray();var timeFields=FindViews<TimePicker>(editor).ToArray();
  if(editor is not EditorPage||dateFields.Length!=2||timeFields.Length!=2||dateFields[0].Date?.Date!=requested.Date||timeFields[0].Time!=requested.TimeOfDay||timeFields[1].Time!=requested.AddHours(1).TimeOfDay)throw new Exception("Quick scheduling must prefill selected date/time");
#if WINDOWS
  await Capture("quick");
#endif
  await Navigation.PopAsync(false);
  System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(),$"orbe-interaction-{Environment.ProcessId}.json"),System.Text.Json.JsonSerializer.Serialize(new{passed=true,selectionWorkMs=milliseconds,calendarControlsReused=42,navigationControlsReused=nav.Length}));
 }
}
#endif
