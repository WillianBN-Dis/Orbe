using Agenda.Core;
using static Agenda.App.OrbeUi;

namespace Agenda.App;

public sealed partial class DashboardPage : ContentPage
{
    private readonly Grid shell = new();
    private readonly Grid main = new() { RowDefinitions = new() { new(GridLength.Auto), new(GridLength.Star) }, RowSpacing = 22 };
    private readonly ContentView sidebarHost = new(), headerHost = new(), mobileHost = new();
    private readonly ScrollView mainScroll = new() { VerticalScrollBarVisibility=ScrollBarVisibility.Never };
    private readonly VerticalStackLayout body = new() { Spacing = 22 };
    private readonly Label status = Text("Seu espaço está pronto para começar.", 12, Muted);
    private readonly ActivityIndicator spinner = new() { Color = Violet, HeightRequest = 20, WidthRequest = 20, IsVisible = false };
    private NexusClient? client;
    private AgendaRepository? repository;
    private IReadOnlyList<Stored<Appointment>> appointments = [];
    private IReadOnlyList<Stored<MoneyEntry>> entries = [];
    private DateOnly selected = DateOnly.FromDateTime(DateTime.Today);
    private DateOnly calendarMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private Period financialPeriod = Period.Mes;
    private EventKind? kindFilter;
    private string tab = "Agenda";
    private bool busy, loaded, demo, dataLoaded, connectionProblem;
    private bool compact;
    private double lastWidth;
    private int resizeVersion;
    private bool Wide => Width >= 1180;
    private bool HasData => demo || dataLoaded;
    private DateOnly Selected => selected;

    public DashboardPage()
    {
        Title = "Orbe"; BackgroundColor = CanvasColor; SafeAreaEdges = SafeAreaEdges.All;
        NavigationPage.SetHasNavigationBar(this, false);
        shell.ColumnDefinitions.Add(new(new GridLength(224))); shell.ColumnDefinitions.Add(new(GridLength.Star));
        shell.RowDefinitions.Add(new(GridLength.Star)); shell.RowDefinitions.Add(new(GridLength.Auto));
        shell.Add(sidebarHost); Grid.SetRowSpan(sidebarHost,2);
        main.Add(headerHost);
        mainScroll.Content=body;main.Add(mainScroll,0,1);
        shell.Add(main,1);shell.Add(mobileHost,1,1);
        Content = shell;
        SizeChanged += (_, _) =>
        {
            if (Width < 1 || Math.Abs(lastWidth - Width) < 32) return;
            var layoutChanged = compact != (Width < 860) || (lastWidth >= 1180) != Wide;
            lastWidth = Width; compact = Width < 860;
            if (loaded && layoutChanged)
            {
                var version=++resizeVersion;
                Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(120),()=>{if(version==resizeVersion)BuildShell();});
            }
        };
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (loaded) return;
        loaded = true; compact = Width < 860; lastWidth = Width;
        if (Environment.GetCommandLineArgs().Contains("--demo") || Environment.GetCommandLineArgs().Contains("--startup-check")) LoadDemo();
        else await Guard(async () =>
        {
            var token = await SecureStorage.Default.GetAsync("nexus-token");
            var url = Preferences.Default.Get("nexus-url", ""); var db = Preferences.Default.Get("nexus-db", "");
            if (!string.IsNullOrEmpty(token) && url.Length > 0 && db.Length > 0)
            { client = new(url, token); repository = new(client, db); await Reload(); }
        });
        BuildShell();
    }
    private void BuildShell()
    {
        if (status.Parent is Layout statusParent) statusParent.Children.Remove(status);
        if (spinner.Parent is Layout spinnerParent) spinnerParent.Children.Remove(spinner);
        shell.ColumnDefinitions[0].Width=compact?new GridLength(0):new GridLength(224);
        sidebarHost.Content=compact?null:BuildSidebar();sidebarHost.IsVisible=!compact;
        main.Padding=new Thickness(compact?18:32,22,compact?18:32,0);
        var eyebrow = Text("ESPAÇO DE TRABALHO", 10, Dim, true); eyebrow.CharacterSpacing = 1.8;
        var header = compact ? Between(Brand(), Pill("Agenda & finanças", Violet)) : Between(eyebrow, Row(10, Icon("calendar", Dim, 16), Text(DateTime.Today.ToString("dddd, dd 'de' MMMM", Finance.Brazilian), 12, Muted)));
        headerHost.Content=header;
        mobileHost.Content=compact?BuildMobileNav():null;mobileHost.IsVisible=compact;
        RenderNow();
        Dispatcher.Dispatch(async()=>await mainScroll.ScrollToAsync(0,0,false));
    }
    private View Brand() => Row(12, new Image { Source="orbe.png", WidthRequest = compact ? 36 : 42, HeightRequest = compact ? 36 : 42 }, Text("orbe", 28, Ink, true));
    private View BuildSidebar()
    {
        var panel = new Grid { BackgroundColor = Sidebar, Padding = new Thickness(20, 26, 20, 20), RowDefinitions = new() { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) } };
        panel.Add(Stack(8, Brand(), Text("TEMPO E FINANÇAS EM SINTONIA", 8, Dim, true)));
        var nav = Stack(8, Text("PRINCIPAL", 10, Dim, true)); nav.Margin = new Thickness(0,40,0,0);
        nav.Add(NavButton("Agenda", "calendar")); nav.Add(NavButton("Financeiro", "chart"));
        nav.Add(new BoxView { HeightRequest = 22, Color = Colors.Transparent });
        nav.Add(Text("SEU ESPAÇO", 10, Dim, true)); nav.Add(NavButton("Conexão", "link"));
        panel.Add(nav, 0, 1);
        var connectionColor=demo?Violet:connectionProblem?Coral:dataLoaded?Mint:Muted;
        var connection = Stack(12, Rule(), Between(Text(demo ? "Modo demonstração" : connectionProblem ? "Nexus indisponível" : dataLoaded ? "Nexus conectado" : "Conectar ao Nexus", 12, connectionColor, true),
            new BoxView { Color = connectionColor, HeightRequest = 6, WidthRequest = 6, CornerRadius = 3, VerticalOptions = LayoutOptions.Center }),
            Text(demo ? "Dados fictícios • somente leitura" : connectionProblem && dataLoaded ? "Exibindo a última leitura disponível." : "Seus dados, no seu computador.", 10, Dim), Text("ORBE  /  1.0", 9, Dim, true));
        panel.Add(connection, 0, 2); return panel;
    }
    private View NavButton(string name, string icon)
    {
        var active = name == tab;
        var button = Button(name, async () => { tab = name; BuildShell(); await AnimateBody(); }, quiet:true);
        button.BackgroundColor = active ? Color.FromArgb("#27233E") : Colors.Transparent;
        button.TextColor = active ? Violet : Muted; button.HorizontalOptions = LayoutOptions.Fill; button.Padding = new Thickness(43,12,10,12);
        var grid = new Grid(); grid.Add(button);
        var glyph = Icon(icon, active ? Violet : Muted, 19); glyph.HorizontalOptions = LayoutOptions.Start; glyph.Margin = new Thickness(14,0,0,0); grid.Add(glyph);
        return grid;
    }
    private View BuildMobileNav()
    {
        var nav = new Grid { BackgroundColor = Sidebar, Padding = new Thickness(10,8), ColumnDefinitions = new() { new(GridLength.Star), new(GridLength.Star), new(GridLength.Star) }, ColumnSpacing = 6 };
        var names = new[] { "Agenda", "Financeiro", "Conexão" };
        for (var i=0;i<names.Length;i++) { var name=names[i]; var b = Button(name, async()=> { tab=name; BuildShell(); await AnimateBody(); }, quiet:true); b.TextColor=name==tab?Violet:Muted; b.BackgroundColor=name==tab?Color.FromArgb("#27233E"):Colors.Transparent; nav.Add(b,i); }
        return nav;
    }
    private void RenderNow()
    {
        if (status.Parent is Layout statusParent) statusParent.Children.Remove(status);
        if (spinner.Parent is Layout spinnerParent) spinnerParent.Children.Remove(spinner);
        body.Children.Clear();
        if (demo)
        {
            var banner=Pill("DEMONSTRAÇÃO · DADOS FICTÍCIOS", Violet);
            var exit=Button("Sair da demonstração", () => Guard(async () => { demo=false; dataLoaded=false; appointments=[]; entries=[]; status.Text="Conecte seu banco para usar seus dados."; if(repository is not null) await Reload(); BuildShell(); }), quiet:true);
            body.Add(compact?Stack(8,banner,exit):Between(banner,exit));
        }
        var title = tab switch { "Agenda" => "Sua agenda", "Financeiro" => "Visão financeira", _ => "Seu espaço, conectado" };
        var subtitle = tab switch { "Agenda" => "Abra espaço para o que importa.", "Financeiro" => "Acompanhe o que entra, o que sai e o que fica.", _ => "Conecte o Orbe ao seu banco de dados Nexus." };
        var heading = Stack(6, Text(title, compact ? 28 : 32, Ink, true), Text(subtitle, 13, Muted));
        var action = tab == "Agenda" ? Button("+  Novo agendamento", () => OpenAppointment(), primary:true) : tab == "Financeiro" ? Button("+  Novo lançamento", () => OpenEntry(), primary:true) : repository is not null ? Button("Atualizar dados", Refresh) : Button("Explorar demonstração",async()=>{LoadDemo();tab="Agenda";BuildShell();await Task.CompletedTask;});
        body.Add(compact ? Stack(16, heading, action) : Between(heading, action));
        if (!HasData && tab != "Conexão") body.Add(Welcome());
        if (tab == "Agenda") RenderAgenda(); else if (tab == "Financeiro") RenderFinance(); else RenderConnection();
        body.Add(Row(8, spinner, status));
        body.Add(new BoxView { HeightRequest=18, Color=Colors.Transparent });
    }
    private View Welcome()
    {
        var text = Stack(6, Text("Tudo começa com uma conexão.", 17, Ink, true), Text("Conecte seu Nexus ou explore o Orbe com dados de exemplo.", 12, Muted));
        var connect = Button("Conectar Nexus", async()=> { tab="Conexão"; BuildShell(); await Task.CompletedTask; }, primary:true);
        var explore = Button("Explorar demonstração", async()=> { LoadDemo(); BuildShell(); await Task.CompletedTask; });
        return Card(compact ? Stack(16,text,Stack(8,connect,explore)) : Between(text,Row(8,connect,explore)), 20, Color.FromArgb("#1D1A30"), Color.FromArgb("#37304F"));
    }
    private async Task Render() { RenderNow(); await AnimateBody(); }
    private Task AnimateBody() { body.AbortAnimation("FadeTo"); body.Opacity=.65; return body.FadeToAsync(1,180,Easing.CubicOut); }
    private async Task Guard(Func<Task> action)
    {
        if (busy) return; busy=true; spinner.IsRunning=spinner.IsVisible=true;
        try { await action(); }
        catch(Exception e) { connectionProblem=true; status.Text="Não foi possível concluir. Confira a conexão."; if(!compact)sidebarHost.Content=BuildSidebar(); await DisplayAlertAsync("Vamos conferir?", FriendlyError(e), "Entendi"); }
        finally { busy=false; spinner.IsRunning=spinner.IsVisible=false; }
    }
    private static string FriendlyError(Exception e) => e is ArgumentException or NexusException ? e.Message : e is OverflowException ? "O valor informado é muito alto. Confira o valor e tente novamente." : "Não foi possível concluir. Confira a conexão e a estrutura do banco. Nenhuma gravação será repetida automaticamente.";
    private async Task Reload()
    {
        if (repository is null) return;
        var nextAppointments=await repository.Appointments(); var nextEntries=await repository.Entries();
        appointments=nextAppointments; entries=nextEntries; demo=false;dataLoaded=true;connectionProblem=false;
        status.Text=$"Atualizado às {DateTime.Now:HH:mm}  ·  {Preferences.Default.Get("nexus-db-name","Nexus")}";
    }
    private Task Refresh() => Guard(async()=> { if(demo) status.Text="Demonstração • os dados não são enviados ao Nexus."; else await Reload(); BuildShell(); });
    private Task NeedConnection() => DisplayAlertAsync(demo ? "Você está na demonstração" : "Conecte seu espaço", demo ? "Os dados de exemplo são somente leitura. Saia da demonstração e conecte seu Nexus para salvar seus agendamentos e lançamentos." : "Na seção Conexão, selecione o banco Nexus para começar a organizar sua rotina.","Entendi");
    private void LoadDemo()
    {
        demo=true; selected=DateOnly.FromDateTime(DateTime.Today); calendarMonth=new(selected.Year,selected.Month,1);
        (appointments, entries)=DemoData.Create(selected); status.Text="Demonstração • dados fictícios, sem acesso aos seus bancos.";
    }
}




