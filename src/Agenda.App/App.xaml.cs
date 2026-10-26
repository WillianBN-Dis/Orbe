using Microsoft.Extensions.DependencyInjection;

namespace Agenda.App;

public partial class App : Application
{
    internal static NavigationPage Navigation(Page page)=>new(page){BarBackgroundColor=Color.FromArgb("#0D101B"),BarTextColor=Colors.White};
    public App() { InitializeComponent(); }
    protected override Window CreateWindow(IActivationState? activationState)
    {
        UserAppTheme=AppTheme.Dark;
        var args=Environment.GetCommandLineArgs();
        var compactPreview=args.Contains("--compact");
        var window=new Window(Navigation(new LoginPage()))
        {Title="Orbe · Agenda e finanças",Width=compactPreview?430:1440,Height=compactPreview?900:960};
        if(args.Contains("--startup-check"))
        {
            window.Created+=(_,_)=>window.Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(4),()=>
            {
                var visible=window.Handler is not null&&window.Width>0&&window.Height>0;
#if WINDOWS
                visible=visible&&window.Handler?.PlatformView is Microsoft.UI.Xaml.Window native&&native.AppWindow.IsVisible&&native.Content is Microsoft.UI.Xaml.FrameworkElement root&&root.ActualWidth>0;
#endif
                var report=System.IO.Path.Combine(System.IO.Path.GetTempPath(),$"orbe-startup-check-{Environment.ProcessId}.json");
                System.IO.File.WriteAllText(report,System.Text.Json.JsonSerializer.Serialize(new{version=AppInfo.Current.VersionString,visible,width=window.Width,height=window.Height}));
                Environment.Exit(visible?0:1);
            });
        }
        return window;
    }
}
