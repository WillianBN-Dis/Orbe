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
#if DEBUG
        // Synthetic layout fixture, compiled out of distributed Release packages.
        if(args.Contains("--layout-check"))
        {
            var qa=new DashboardPage(new Agenda.Core.CloudApi(Agenda.Core.CloudApi.DefaultUrl),new Agenda.Core.CloudProfile("qa","Equipe QA","qa@example.test",null,null,true,["Owner"],null));
            window.Page=Navigation(qa);
            if(args.Contains("--interaction-check"))window.Created+=(_,_)=>window.Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(1),async()=>{try{await qa.QaInteractions();}catch(Exception e){System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(),$"orbe-interaction-{Environment.ProcessId}.json"),e.ToString());Environment.Exit(1);}});
        }
        if(args.Contains("--startup-check")||args.Contains("--layout-check"))
        {if(double.TryParse(Environment.GetEnvironmentVariable("ORBE_QA_WIDTH"),out var qaWidth))window.Width=qaWidth;if(double.TryParse(Environment.GetEnvironmentVariable("ORBE_QA_HEIGHT"),out var qaHeight))window.Height=qaHeight;}
#endif
        if(args.Contains("--startup-check"))
        {
            window.Created+=(_,_)=>window.Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(4),async()=>
            {
                var visible=window.Handler is not null&&window.Width>0&&window.Height>0;
#if WINDOWS
                visible=visible&&window.Handler?.PlatformView is Microsoft.UI.Xaml.Window native&&native.AppWindow.IsVisible&&native.Content is Microsoft.UI.Xaml.FrameworkElement root&&root.ActualWidth>0;
#endif
                var report=System.IO.Path.Combine(System.IO.Path.GetTempPath(),$"orbe-startup-check-{Environment.ProcessId}.json");
                string? screenshot=null;string? captureError=null;
#if WINDOWS
                try{if(window.Handler?.PlatformView is Microsoft.UI.Xaml.Window captureWindow&&captureWindow.Content is Microsoft.UI.Xaml.FrameworkElement captureRoot){screenshot=System.IO.Path.ChangeExtension(report,"png");await StartupCapture.Capture(captureRoot,screenshot);}}
                catch(Exception e){captureError=e.GetType().Name;screenshot=null;}
#endif
                System.IO.File.WriteAllText(report,System.Text.Json.JsonSerializer.Serialize(new{version=AppInfo.Current.VersionString,visible,width=window.Width,height=window.Height,screenshot,captureError}));
                Environment.Exit(visible&&captureError is null?0:1);
            });
        }
#if DEBUG && WINDOWS
        if(args.Contains("--live-check"))window.Created+=(_,_)=>window.Dispatcher.Dispatch(async()=>await RunLiveCheck(window));
#endif
        return window;
    }
}
