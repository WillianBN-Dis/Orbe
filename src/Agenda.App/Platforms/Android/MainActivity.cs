using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Content;

namespace Agenda.App;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
#if DEBUG
        // Synthetic local UI fixture; absent from distributed Release builds.
        var form=Intent?.GetStringExtra("orbe.qa.form");
        if(form is "hours" or "block" or "services"){
            System.Environment.SetEnvironmentVariable("ORBE_QA_FORM",form);
            System.Environment.SetEnvironmentVariable("ORBE_QA_FORMS","true");
        }
#endif
        OpenInbox(Intent);
        base.OnCreate(savedInstanceState);
    }
    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        OpenInbox(intent);
    }
    private static void OpenInbox(Intent? intent)
    {
        if(intent?.GetStringExtra("orbe_notifications")!="1")return;
        intent.RemoveExtra("orbe_notifications");
        InboxActivation.Request();
    }
}
