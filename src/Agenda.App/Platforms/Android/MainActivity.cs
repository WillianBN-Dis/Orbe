using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Content;

namespace Agenda.App;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, WindowSoftInputMode = Android.Views.SoftInput.AdjustResize, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
#if DEBUG
        if(Intent?.GetBooleanExtra("orbe.qa.price-notice",false)==true){
            System.Environment.SetEnvironmentVariable("ORBE_QA_PRICE_NOTICE","true");
            System.Environment.SetEnvironmentVariable("ORBE_QA_NOTICES","true");
        }
        if(Intent?.GetBooleanExtra("orbe.qa.scroll",false)==true)
            System.Environment.SetEnvironmentVariable("ORBE_QA_SCROLL","true");
        // Synthetic local UI fixture; absent from distributed Release builds.
        var form=Intent?.GetStringExtra("orbe.qa.form");
        if(form is "hours" or "block" or "services" or "service-edit"){
            System.Environment.SetEnvironmentVariable("ORBE_QA_FORM",form);
            System.Environment.SetEnvironmentVariable("ORBE_QA_FORMS","true");
        }
#endif
        OpenInbox(Intent);
        base.OnCreate(savedInstanceState);
        Window?.SetSoftInputMode(Android.Views.SoftInput.AdjustResize);
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
