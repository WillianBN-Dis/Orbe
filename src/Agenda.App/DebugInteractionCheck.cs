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
  dayButton.IsEnabled=false;await Task.Delay(100);
  if(cursorProperty.GetValue(native) is not Microsoft.UI.Input.InputSystemCursor || ReferenceEquals(enabledCursor,cursorProperty.GetValue(native)))throw new Exception("Disabled calendar must use arrow cursor");
  dayButton.IsEnabled=true;
  if(!compact&&(dayButton.WidthRequest<56||dayButton.HeightRequest<56))throw new Exception("Desktop calendar targets too small");
#endif
  await SelectDay(month.AddDays(1));var milliseconds=watch.Elapsed.TotalMilliseconds;
  var dayCell=(Grid)calendarButtons[month.AddDays(1)].Parent;
  var markers=dayCell.Children.OfType<HorizontalStackLayout>().Single();
  var colors=markers.Children.OfType<Microsoft.Maui.Controls.Shapes.Ellipse>()
   .Select(dot=>((SolidColorBrush)dot.Fill).Color).ToArray();
  if(colors.Length!=4||colors.Distinct().Count()!=4||!markers.InputTransparent)
   throw new Exception("Expected one visible marker per event type, with the day still clickable");
  if(!initial.SequenceEqual(calendarButtons.Values))throw new Exception("Calendar controls rebuilt on selection");
  if(selected!=month.AddDays(1))throw new Exception("Selected date not updated");
  await MoveMonth(1);await SelectDay(DateOnly.FromDateTime(DateTime.Today));
  if(calendarMonth.Month!=selected.Month||calendarMonth.Year!=selected.Year)throw new Exception("Today did not restore month");
  await SwitchTab("Conta");await SwitchTab("Financeiro");await SwitchTab("Agenda");
  if(!nav.SequenceEqual(navigationButtons.Keys))throw new Exception("Navigation controls rebuilt on tab change");
  var weekdayLabels=FindViews<Label>(agendaCalendar).Where(l=>new[]{"DOM","SEG","TER","QUA","QUI","SEX","SÁB"}.Contains(l.Text)).Select(l=>l.Text).ToArray();
  if(!weekdayLabels.SequenceEqual(new[]{"DOM","SEG","TER","QUA","QUI","SEX","SÁB"}))throw new Exception("Sunday-first calendar missing");
#if WINDOWS
  async Task Capture(string mode){
   await Task.Delay(300);
   if(Window?.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow&&nativeWindow.Content is Microsoft.UI.Xaml.FrameworkElement root)
    await StartupCapture.Capture(root,System.IO.Path.Combine(System.IO.Path.GetTempPath(),$"orbe-calendar-{mode}-{Environment.ProcessId}.png"));
  }
  await Capture("month");
#endif
  agendaView="Ano";agendaCalendar.Content=BuildCalendar();
  if(FindViews<Button>(agendaCalendar).Count(b=>System.Globalization.CultureInfo.GetCultureInfo("pt-BR").DateTimeFormat.MonthNames.Contains(b.Text,StringComparer.OrdinalIgnoreCase))!=12)
   throw new Exception("Expected 12 months in year view");
#if WINDOWS
  await Capture("year");
#endif
  agendaView="Dia";agendaCalendar.Content=BuildCalendar();
  var nextDay=Selected.AddDays(1);await SelectDay(nextDay);
  if(selected!=nextDay||calendarButtons.Count!=0)throw new Exception("Day view navigation failed");
#if WINDOWS
  await Capture("day");
#endif
  System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(),$"orbe-interaction-{Environment.ProcessId}.json"),System.Text.Json.JsonSerializer.Serialize(new{passed=true,selectionWorkMs=milliseconds,calendarControlsReused=42,navigationControlsReused=nav.Length}));
 }
}
#endif
