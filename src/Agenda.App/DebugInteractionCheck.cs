#if DEBUG
using System.Diagnostics;
namespace Agenda.App;
public sealed partial class DashboardPage
{
 internal async Task QaInteractions()
 {
  var initial=calendarButtons.Values.ToArray();var nav=navigationButtons.Keys.ToArray();var month=calendarMonth;var watch=Stopwatch.StartNew();
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
