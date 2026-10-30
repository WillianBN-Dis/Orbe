using Agenda.Core;
using System.Net;
using static Agenda.App.OrbeUi;

namespace Agenda.App;
public sealed partial class DashboardPage : ContentPage
{
    private readonly Grid shell=new();
    private readonly Grid main=new(){RowDefinitions=new(){new(GridLength.Auto),new(GridLength.Star)},RowSpacing=22};
    private readonly ContentView sidebarHost=new(),headerHost=new(),mobileHost=new();
    private readonly ScrollView mainScroll=new(){VerticalScrollBarVisibility=ScrollBarVisibility.Never};
    private readonly VerticalStackLayout body=new(){Spacing=22};
    private readonly Label status=Text("Atualizando seu espaço…",12,Muted);
    private readonly ActivityIndicator spinner=new(){Color=Violet,HeightRequest=20,WidthRequest=20,IsVisible=false};
    private readonly CloudApi client;
    private CloudProfile profile;
    private CloudAgendaRepository? repository;
    private CloudEmployee[] people=[];
    private IReadOnlyList<Stored<Appointment>> appointments=[];
    private IReadOnlyList<Stored<MoneyEntry>> entries=[];
    private DateOnly selected=DateOnly.FromDateTime(DateTime.Today),calendarMonth=new(DateTime.Today.Year,DateTime.Today.Month,1);
    private Period financialPeriod=Period.Mes;
    private EventKind? kindFilter;
    private string tab="Agenda";
    private bool busy,loaded,dataLoaded,connectionProblem,compact;
    private double lastWidth;
    private int resizeVersion;
    private IDispatcherTimer? liveTimer;
    private Window? observedWindow;
    private bool activePage;
    private bool Wide=>Width>=1180;
    private bool HasData=>dataLoaded;
#if DEBUG
    internal bool QaDataReady=>loaded&&dataLoaded;
    internal int QaAppointmentCount=>appointments.Count;
    internal void QaSelectFirst(){if(appointments.Count>0){selected=DateOnly.FromDateTime(appointments[0].Value.Start);calendarMonth=new(selected.Year,selected.Month,1);BuildShell();}}
#endif
    private DateOnly Selected=>selected;
    public DashboardPage(CloudApi api,CloudProfile account)
    {
        client=api;profile=account;Title="Orbe";BackgroundColor=CanvasColor;SafeAreaEdges=SafeAreaEdges.All;
        NavigationPage.SetHasNavigationBar(this,false);
        shell.ColumnDefinitions.Add(new(new GridLength(224)));shell.ColumnDefinitions.Add(new(GridLength.Star));
        shell.RowDefinitions.Add(new(GridLength.Star));shell.RowDefinitions.Add(new(GridLength.Auto));
        shell.Add(sidebarHost);Grid.SetRowSpan(sidebarHost,2);main.Add(headerHost);mainScroll.Content=body;main.Add(mainScroll,0,1);shell.Add(main,1);shell.Add(mobileHost,1,1);Content=shell;
        SizeChanged+=(_,_)=>{if(Width<1||Math.Abs(lastWidth-Width)<32)return;var changed=compact!=(Width<860)||(lastWidth>=1180)!=Wide;lastWidth=Width;compact=Width<860;var gutter=compact?18:Math.Max(32,(Width-224-1920)/2);main.Padding=new Thickness(gutter,22,gutter,0);if(loaded&&changed){var version=++resizeVersion;Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(120),()=>{if(version==resizeVersion)BuildShell();});}};
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();compact=Width<860;lastWidth=Width;
#if DEBUG
        if(Environment.GetCommandLineArgs().Contains("--layout-check")){loaded=dataLoaded=true;tab=Environment.GetEnvironmentVariable("ORBE_QA_TAB")??"Agenda";BuildShell();return;}
#endif
        await Guard(async()=>
        {
            profile=await client.Me();await client.ConfigureZone();people=(await client.Get<CloudEmployee[]>("team/employees")).Where(x=>profile.Owner||x.Enabled).ToArray();
            if(!loaded)
            {
                var employee=profile.Owner?Preferences.Default.Get("orbe-employee-id",""):profile.EmployeeId;
#if DEBUG
                if(Environment.GetCommandLineArgs().Contains("--live-check"))employee=profile.EmployeeId;
#endif
                if(!string.IsNullOrEmpty(employee)&&people.All(x=>x.Id!=employee))employee=null;
                repository=new(client,string.IsNullOrEmpty(employee)?null:employee);
                selected=DateOnly.FromDateTime(client.Local(DateTimeOffset.UtcNow));calendarMonth=new(selected.Year,selected.Month,1);loaded=true;
            }
            await Reload();BuildShell();StartLiveUpdates();
        });
        if(loaded){BuildShell();StartLiveUpdates();}
    }
    protected override void OnDisappearing(){activePage=false;liveTimer?.Stop();if(observedWindow is not null){observedWindow.Resumed-=ResumeUpdates;observedWindow.Stopped-=PauseUpdates;observedWindow=null;}base.OnDisappearing();}
    private void StartLiveUpdates()
    {
        activePage=true;liveTimer??=Dispatcher.CreateTimer();liveTimer.IsRepeating=true;liveTimer.Interval=TimeSpan.FromSeconds(15);
        liveTimer.Tick-=LiveTick;liveTimer.Tick+=LiveTick;liveTimer.Start();
        if(observedWindow!=Window){if(observedWindow is not null){observedWindow.Resumed-=ResumeUpdates;observedWindow.Stopped-=PauseUpdates;}observedWindow=Window;if(observedWindow is not null){observedWindow.Resumed+=ResumeUpdates;observedWindow.Stopped+=PauseUpdates;}}
    }
    private void PauseUpdates(object? sender,EventArgs e)=>liveTimer?.Stop();
    private async void ResumeUpdates(object? sender,EventArgs e){if(!activePage)return;liveTimer?.Start();await LiveReload();}
    private async void LiveTick(object? sender,EventArgs e)=>await LiveReload();
    private async Task LiveReload()
    {
        if(!activePage||busy||repository is null||!ReferenceEquals(Navigation.NavigationStack.LastOrDefault(),this))return;
        busy=true;
        try
        {
            var connection=await CloudRouting.Discover(bootstrap:
#if DEBUG
                Environment.GetCommandLineArgs().Contains("--live-check")?client.BaseUrl:
#endif
                CloudApi.DefaultUrl);
            if(connection.ApiUrl!=client.BaseUrl){await CloudSessionStore.End(this,client);return;}
            var oldAppointments=appointments.Select(x=>x.Value).ToArray();var oldEntries=entries.Select(x=>x.Value).ToArray();var wasOffline=connectionProblem;
            await Reload();
            if(wasOffline||!oldAppointments.SequenceEqual(appointments.Select(x=>x.Value))||!oldEntries.SequenceEqual(entries.Select(x=>x.Value)))
            {var scroll=mainScroll.ScrollY;RenderNow();if(!compact)sidebarHost.Content=BuildSidebar();await mainScroll.ScrollToAsync(0,scroll,false);}
        }
        catch(CloudException e)when(e.Status==HttpStatusCode.Unauthorized){appointments=[];entries=[];await CloudSessionStore.End(this,client);}
        catch(Exception){connectionProblem=true;status.Text="Sem conexão no momento. Tentaremos atualizar novamente automaticamente.";}
        finally{busy=false;}
    }
    private readonly Dictionary<Button,string> navigationButtons=new();
    private readonly List<(GraphicsView Glyph,string Tab)> navigationGlyphs=[];
    private Task SwitchTab(string name)
    {
        if(tab==name)return Task.CompletedTask;tab=name;
        foreach(var item in navigationButtons){item.Key.TextColor=item.Value==tab?Violet:Muted;item.Key.BackgroundColor=item.Value==tab?Color.FromArgb("#27233E"):Colors.Transparent;}
        foreach(var item in navigationGlyphs){item.Glyph.Drawable=new LineIcon(item.Tab=="Agenda"?"calendar":item.Tab=="Financeiro"?"chart":item.Tab=="Notificações"?"bell":"people",item.Tab==tab?Violet:Muted);item.Glyph.Invalidate();}
        RenderNow();_ = mainScroll.ScrollToAsync(0,0,false);return Task.CompletedTask;
    }
    private void BuildShell()
    {
        navigationButtons.Clear();navigationGlyphs.Clear();
        if(status.Parent is Layout parent)parent.Children.Remove(status);if(spinner.Parent is Layout spinParent)spinParent.Children.Remove(spinner);
        shell.ColumnDefinitions[0].Width=compact?new GridLength(0):new GridLength(224);sidebarHost.Content=compact?null:BuildSidebar();sidebarHost.IsVisible=!compact;
        // Keep the workspace useful on large monitors without stretching every control edge to edge.
        var desktopGutter=Math.Max(32,(Width-224-1920)/2);
        main.Padding=new Thickness(compact?18:desktopGutter,22,compact?18:desktopGutter,0);
        var eyebrow=Text("ESPAÇO DE TRABALHO",10,Dim,true);eyebrow.CharacterSpacing=1.8;
        var scope=repository?.EmployeeId is {} id?people.FirstOrDefault(x=>x.Id==id)?.DisplayName:profile.Owner?"Visão geral":profile.FullName;
        headerHost.Content=compact?Between(Brand(),Pill(scope??profile.FullName,Violet)):Between(eyebrow,Row(10,Icon("people",Dim,16),Text(scope??profile.FullName,12,Muted)));
        mobileHost.Content=compact?BuildMobileNav():null;mobileHost.IsVisible=compact;RenderNow();Dispatcher.Dispatch(async()=>await mainScroll.ScrollToAsync(0,0,false));
    }
    private View Brand()=>Row(12,BrandImage(compact?30:42),Text("orbe",compact?24:28,Ink,true));
    private View BuildSidebar()
    {
        var panel=new Grid{BackgroundColor=Sidebar,Padding=new Thickness(20,26,20,20),RowDefinitions=new(){new(GridLength.Auto),new(GridLength.Star),new(GridLength.Auto)}};
        panel.Add(Stack(8,Brand(),Text("TEMPO E FINANÇAS EM SINTONIA",8,Dim,true)));
        var nav=Stack(8,Text("PRINCIPAL",10,Dim,true));nav.Margin=new Thickness(0,40,0,0);nav.Add(NavButton("Agenda","calendar"));nav.Add(NavButton("Financeiro","chart"));nav.Add(NavButton("Notificações","bell"));nav.Add(new BoxView{HeightRequest=22,Color=Colors.Transparent});nav.Add(Text("SEU ESPAÇO",10,Dim,true));nav.Add(NavButton("Conta","people"));panel.Add(nav,0,1);
        var color=connectionProblem?Coral:dataLoaded?Mint:Muted;panel.Add(Stack(12,Rule(),Text(connectionProblem?"Conexão indisponível":dataLoaded?"Orbe conectado":"Atualizando seu espaço",12,color,true),Text(profile.FullName,11,Muted),Text(connectionProblem&&dataLoaded?"Exibindo a última leitura disponível.":"Seus dados, protegidos pela sua conta.",10,Dim),Text("ORBE / "+AppInfo.Current.VersionString,9,Dim,true)),0,2);return panel;
    }
    private View NavButton(string name,string icon)
    {
        var active=name==tab;var b=Button(name,()=>SwitchTab(name),quiet:true);b.BackgroundColor=active?Color.FromArgb("#27233E"):Colors.Transparent;b.TextColor=active?Violet:Muted;b.HorizontalOptions=LayoutOptions.Fill;b.Padding=new Thickness(43,12,10,12);
        navigationButtons[b]=name;var grid=new Grid();grid.Add(b);var glyph=Icon(icon,active?Violet:Muted,19);navigationGlyphs.Add((glyph,name));glyph.HorizontalOptions=LayoutOptions.Start;glyph.Margin=new Thickness(14,0,0,0);grid.Add(glyph);return grid;
    }
    private View BuildMobileNav()
    {
        var nav=new Grid{BackgroundColor=Sidebar,Padding=new Thickness(6,8),ColumnSpacing=4};
        var names=new[]{"Agenda","Financeiro","Notificações","Conta"};for(int i=0;i<names.Length;i++){nav.ColumnDefinitions.Add(new(GridLength.Star));var name=names[i];var b=Button(name,()=>SwitchTab(name),quiet:true);b.FontSize=11;b.Padding=new Thickness(4,10);navigationButtons[b]=name;b.HorizontalOptions=LayoutOptions.Fill;b.TextColor=name==tab?Violet:Muted;b.BackgroundColor=name==tab?Color.FromArgb("#27233E"):Colors.Transparent;nav.Add(b,i);}return nav;
    }
    private void RenderNow()
    {
        if(status.Parent is Layout parent)parent.Children.Remove(status);if(spinner.Parent is Layout spinParent)spinParent.Children.Remove(spinner);body.Children.Clear();
        if(tab=="Notificações"){body.Add(Stack(8,Text("Notificações",32,Ink,true),Text("Seu centro de novidades",14,Muted)));body.Add(Card(Stack(18,Icon("bell",Violet,48),Pill("EM BREVE",Violet),Text("Tudo importante, em um só lugar",23,Ink,true),Text("Este espaço está preparado para as notificações do Orbe. O envio de avisos ainda não está disponível.",14,Muted)),32));return;}
        var title=tab switch{"Agenda"=>"Sua agenda","Financeiro"=>"Visão financeira",_=>"Seu espaço, sua conta"};
        var subtitle=tab switch{"Agenda"=>"Abra espaço para o que importa.","Financeiro"=>"Acompanhe o que entra, o que sai e o que fica.",_=>"Seu perfil, sua disponibilidade e seus acessos."};
        var heading=Stack(6,Text(title,compact?28:32,Ink,true),Text(subtitle,13,Muted));
        var action=tab=="Agenda"?Button("+  Novo agendamento",()=>OpenAppointment(),primary:true):tab=="Financeiro"?Button("+  Novo lançamento",()=>OpenEntry(),primary:true):Button("Atualizar dados",Refresh);
        body.Add(compact?Stack(16,heading,action):Between(heading,action));
        if((!HasData||people.Length==0)&&tab!="Conta")body.Add(Welcome());
        if(tab=="Agenda")RenderAgenda();else if(tab=="Financeiro")RenderFinance();else RenderAccount();
        body.Add(Row(8,spinner,status));body.Add(new BoxView{HeightRequest=18,Color=Colors.Transparent});
    }
    private View Welcome()=>Card(Stack(12,Text(profile.Owner?"Vamos preparar sua equipe?":"Seu espaço está sendo preparado.",18,Ink,true),Text(profile.Owner?"Cadastre os profissionais e serviços, importe seu histórico e configure as jornadas antes de abrir as reservas online.":"Sua agenda depende da liberação do seu acesso profissional. Entre em contato com WillianBN.",12,Muted),Button("Abrir minha conta",()=>{tab="Conta";BuildShell();return Task.CompletedTask;},primary:true)),20,Color.FromArgb("#1D1A30"),Color.FromArgb("#37304F"));
    private Task Render(){RenderNow();return Task.CompletedTask;}
    private async Task Guard(Func<Task> action)
    {
        if(busy)return;busy=true;spinner.IsRunning=spinner.IsVisible=true;
        try{await action();}
        catch(CloudException e)when(e.Status==HttpStatusCode.Unauthorized){appointments=[];entries=[];await CloudSessionStore.End(this,client);}
        catch(Exception e){connectionProblem=true;status.Text="Não foi possível concluir. Confira sua conexão.";if(!compact)sidebarHost.Content=BuildSidebar();await DisplayAlertAsync("Vamos conferir?",FriendlyError(e),"Entendi");}
        finally{busy=false;spinner.IsRunning=spinner.IsVisible=false;}
    }
    private static string FriendlyError(Exception e)=>e is ArgumentException or CloudException?e.Message:e is OverflowException?"O valor informado é muito alto. Confira e tente novamente.":"Não foi possível concluir. Atualize os dados antes de tentar novamente. Nenhuma gravação será repetida automaticamente.";
    private async Task Reload()
    {
        if(repository is null)return;var nextAppointments=await repository.Appointments();var nextEntries=await repository.Entries();appointments=nextAppointments;entries=nextEntries;dataLoaded=true;connectionProblem=false;status.Text=$"Atualizado às {client.Local(DateTimeOffset.UtcNow):HH:mm}";
    }
    private Task Refresh()=>Guard(async()=>{await Reload();BuildShell();});
    private Task NeedConnection()=>DisplayAlertAsync("Escolha um profissional","Na seção Conta, selecione o profissional cuja agenda deseja organizar.","Entendi");
}
