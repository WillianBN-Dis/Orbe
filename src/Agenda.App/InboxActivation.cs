namespace Agenda.App;

internal static class InboxActivation
{
 private static bool pending;
 internal static event Action? Requested;
 internal static async void Request()
 {
  pending=true;
  if(Application.Current?.Windows.FirstOrDefault()?.Page is NavigationPage navigation&&
     navigation.RootPage is DashboardPage&&navigation.CurrentPage is not DashboardPage)
  {
   await navigation.PopToRootAsync();
  }
  Requested?.Invoke();
 }
 internal static bool Consume(){if(!pending)return false;pending=false;return true;}
}
