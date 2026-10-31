using Android.App;
using Android.Content.PM;
using Android.OS;

namespace Agenda.App;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
#if DEBUG
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Synthetic local UI fixture; absent from distributed Release builds.
        var form=Intent?.GetStringExtra("orbe.qa.form");
        if(form is "hours" or "block" or "services"){
            System.Environment.SetEnvironmentVariable("ORBE_QA_FORM",form);
            System.Environment.SetEnvironmentVariable("ORBE_QA_FORMS","true");
        }
        base.OnCreate(savedInstanceState);
    }
#endif
}
