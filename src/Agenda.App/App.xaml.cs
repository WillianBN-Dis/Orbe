using Microsoft.Extensions.DependencyInjection;

namespace Agenda.App;

public partial class App : Application
{
    private bool updateCheckStarted;
    private async Task CheckUpdates(Window window)
    {
        if(updateCheckStarted||window.Page is null)return;
        updateCheckStarted=true;
        try
        {
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(12));
            using var service=new Agenda.Core.AppUpdates(windows:DeviceInfo.Current.Platform==DevicePlatform.WinUI);
            var update=await service.Check(timeout.Token);
            if(!int.TryParse(AppInfo.Current.BuildString,out var current)||update.Build<=current)return;
            if(window.Handler is null)return;
            var page=window.Page is NavigationPage navigation?navigation.CurrentPage:window.Page;
            if(page is null||page is UpdatePage)return;
            if(await page.DisplayAlertAsync("Atualização disponível","Orbe "+update.Version+" está disponível. Deseja ver a atualização?","Ver atualização","Agora não"))
                await page.Navigation.PushAsync(new UpdatePage());
        }
        catch(Exception e) when(e is HttpRequestException or OperationCanceledException or System.IO.InvalidDataException or System.Text.Json.JsonException or InvalidOperationException)
        { /* Background checks must not interrupt access when the feed is unavailable. */ }
    }
    internal static NavigationPage Navigation(Page page)=>new(page){BarBackgroundColor=Color.FromArgb("#0D101B"),BarTextColor=Colors.White};
    public App() { InitializeComponent(); }
    protected override Window CreateWindow(IActivationState? activationState)
    {
        UserAppTheme=AppTheme.Dark;
        var args=Environment.GetCommandLineArgs();
        var compactPreview=args.Contains("--compact");
        var window=new Window(Navigation(new LoginPage()))
        {Title="Orbe · Agenda e finanças",Width=compactPreview?430:1440,Height=compactPreview?900:960};
        window.Created+=(_,_)=>window.Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(8),async()=>await CheckUpdates(window));
#if DEBUG
        if(args.Contains("--forms-check")||Environment.GetEnvironmentVariable("ORBE_QA_FORMS")=="true"){
            var form=DebugFormFixture.Create();window.Page=Navigation(form);
            window.Created+=(_,_)=>window.Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(1),async()=>{try{await DebugFormFixture.Verify(form);}catch(Exception e){System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(),$"orbe-forms-{Environment.ProcessId}.json"),e.ToString());Environment.Exit(1);}});
        }
        if(args.Contains("--login-check")&&window.Page is NavigationPage {CurrentPage:LoginPage login})
            window.Created+=(_,_)=>window.Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(1),async()=>{try{
#if WINDOWS
                await login.QaDesktopLogin();
#endif
            }catch(Exception e){System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(),$"orbe-login-check-{Environment.ProcessId}.json"),e.ToString());Environment.Exit(1);}});
        // Synthetic layout fixture, compiled out of distributed Release packages.
        if(args.Contains("--layout-check"))
        {
            var qa=new DashboardPage(Environment.GetEnvironmentVariable("ORBE_QA_NOTICES")=="true"?DebugNotificationFixture.Api():new Agenda.Core.CloudApi(Agenda.Core.CloudApi.DefaultUrl),new Agenda.Core.CloudProfile("qa","Equipe QA","qa@example.test",null,null,true,["Owner"],null));
            window.Page=Navigation(qa);
            if(Environment.GetEnvironmentVariable("ORBE_QA_NOTICES")=="true")window.Created+=(_,_)=>window.Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(1),async()=>{try{await qa.QaNotificationCheck(args.Contains("--notification-check"));}catch(Exception e){System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(),$"orbe-notifications-{Environment.ProcessId}.json"),e.ToString());Environment.Exit(1);}});
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
