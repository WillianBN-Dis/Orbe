#if DEBUG
using System.Diagnostics;
namespace Agenda.App;
public sealed partial class DashboardPage
{
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
  if(!initial.SequenceEqual(calendarButtons.Values))throw new Exception("Calendar controls rebuilt on selection");
  if(selected!=month.AddDays(1))throw new Exception("Selected date not updated");
  await MoveMonth(1);await SelectDay(DateOnly.FromDateTime(DateTime.Today));
  if(calendarMonth.Month!=selected.Month||calendarMonth.Year!=selected.Year)throw new Exception("Today did not restore month");
  await SwitchTab("Conta");await SwitchTab("Financeiro");await SwitchTab("Agenda");
  if(!nav.SequenceEqual(navigationButtons.Keys))throw new Exception("Navigation controls rebuilt on tab change");
  System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(),$"orbe-interaction-{Environment.ProcessId}.json"),System.Text.Json.JsonSerializer.Serialize(new{passed=true,selectionWorkMs=milliseconds,calendarControlsReused=42,navigationControlsReused=nav.Length}));
 }
}
#endif
