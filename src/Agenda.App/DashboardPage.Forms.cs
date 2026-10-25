using Agenda.Core;
using static Agenda.App.OrbeUi;

namespace Agenda.App;

public sealed partial class DashboardPage
{
    private Task OpenAppointment(Stored<Appointment>? original=null)
    {
        if(!demo&&repository is null)return NeedConnection();
        var a=original?.Value;var eventId=a?.Id??Guid.NewGuid().ToString("N");
        var title=Input("Ex.: Atendimento individual",a?.Title);var person=Input("Nome da pessoa ou cliente",a?.Client);
        var notes=new Editor{Text=a?.Notes,Placeholder="Algum detalhe importante para este compromisso?",AutoSize=EditorAutoSizeOption.TextChanges,MinimumHeightRequest=90,TextColor=Ink,PlaceholderColor=Dim,FontSize=14};
        var kind=new Picker{Title="Escolha o tipo",ItemsSource=Enum.GetNames<EventKind>(),SelectedIndex=(int)(a?.Kind??EventKind.Atendimento)};
        var from=new DatePicker{Date=a?.Start??Selected.ToDateTime(new(9,0)),Format="dd/MM/yyyy"};
        var fromTime=new TimePicker{Time=a?.Start.TimeOfDay??TimeSpan.FromHours(9),Format="HH:mm"};
        var until=new DatePicker{Date=a?.End??Selected.ToDateTime(new(10,0)),Format="dd/MM/yyyy"};
        var untilTime=new TimePicker{Time=a?.End.TimeOfDay??TimeSpan.FromHours(10),Format="HH:mm"};
        var amount=Input("0,00",((a?.ExpectedCents??0)/100m).ToString("F2",Finance.Brazilian));amount.Keyboard=Keyboard.Numeric;
        var fields=new View[]{Field("Título do agendamento",title),Columns(!compact,Field("Tipo de evento",kind),Field("Pessoa ou cliente · opcional",person)),Rule(),
            Text("Quando acontece?",18,Ink,true),Columns(!compact,Field("Data de início",from),Field("Horário de início",fromTime)),
            Columns(!compact,Field("Data de término",until),Field("Horário de término",untilTime)),Rule(),
            Field("Valor previsto · R$",amount,"Previsão para este evento. O recebimento é registrado separadamente."),Field("Observações · opcional",notes)};
        return OpenForm(a is null?"Novo agendamento":"Editar agendamento","Reserve um momento na sua rotina.","calendar",fields,async()=>
        {
            if(demo||repository is null){await NeedConnection();return false;}
            var value=new Appointment(eventId,title.Text??"",(EventKind)kind.SelectedIndex,(from.Date??DateTime.Today).Date+(fromTime.Time??TimeSpan.Zero),
                (until.Date??DateTime.Today).Date+(untilTime.Time??TimeSpan.Zero),person.Text,notes.Text,Finance.ParseCents(amount.Text??"0"));value.Validate();
            if(appointments.Any(x=>x.Value.Id!=value.Id&&x.Value.Start<value.End&&x.Value.End>value.Start)&&
                !await DisplayAlertAsync("Já existe um evento neste horário","Você pode manter os dois compromissos. Deseja salvar mesmo assim?","Salvar mesmo assim","Revisar horário"))return false;
            await repository.Save(value,original?.Snapshot);return true;
        });
    }
    private Task OpenEntry(Appointment? appointment=null)
    {
        if(!demo&&repository is null)return NeedConnection();
        var description=Input("Ex.: Recebimento de atendimento",appointment?.Title);
        var kind=new Picker{Title="Tipo",ItemsSource=new[]{"Recebimento","Despesa"},SelectedIndex=0};
        var when=new DatePicker{Date=Selected.ToDateTime(TimeOnly.MinValue),Format="dd/MM/yyyy"};
        var amount=Input("0,00",appointment is null?"":(appointment.ExpectedCents/100m).ToString("F2",Finance.Brazilian));amount.Keyboard=Keyboard.Numeric;
        var id=Guid.NewGuid().ToString("N");
        var choices=appointments.Select(x=>x.Value).OrderBy(x=>x.Start).ToArray();
        var linked=new Picker{Title="Agendamento vinculado",ItemsSource=new[]{"Sem vínculo"}.Concat(choices.Select(x=>$"{x.Start:dd/MM/yyyy HH:mm} · {x.Title}")).ToArray(),SelectedIndex=appointment is null?0:Array.FindIndex(choices,x=>x.Id==appointment.Id)+1};
        return OpenForm("Novo lançamento","Cada movimento deixa suas finanças mais claras.","wallet",[
            Field("Descrição",description),Columns(!compact,Field("Tipo de movimentação",kind),Field("Data efetiva",when)),
            Field("Valor · R$",amount,"Informe o valor efetivamente recebido ou gasto."),
            Field("Agendamento vinculado · opcional",linked,"Excluir o agendamento também excluirá os lançamentos vinculados a ele. Sem vínculo, o lançamento será mantido."),
            Card(Text("Este lançamento será incluído no resultado do dia, mês e ano da data escolhida.",12,Muted),16,CanvasColor)],async()=>
        {
            if(demo||repository is null){await NeedConnection();return false;}
            await repository.Save(new MoneyEntry(id,description.Text??"",(EntryKind)kind.SelectedIndex,DateOnly.FromDateTime(when.Date??DateTime.Today),Finance.ParseCents(amount.Text??""),linked.SelectedIndex>0?choices[linked.SelectedIndex-1].Id:null));return true;
        });
    }
    private async Task OpenForm(string title,string subtitle,string glyph,View[] fields,Func<Task<bool>> save)
    {
        var page=new EditorPage{Title=title,BackgroundColor=CanvasColor,SafeAreaEdges=SafeAreaEdges.All};NavigationPage.SetHasNavigationBar(page,false);
        var root=new Grid{RowDefinitions=new(){new(GridLength.Auto),new(GridLength.Star),new(GridLength.Auto)},Padding=new Thickness(compact?18:32,18),RowSpacing=20,MaximumWidthRequest=780,HorizontalOptions=LayoutOptions.Fill};
        var back=Button("‹  Voltar",async()=>{await Navigation.PopAsync();},quiet:true);
        var cancel=Button("Cancelar",async()=>{await Navigation.PopAsync();});
        var heading=Stack(8,back,Row(12,Card(Icon(glyph,Violet,24),12,Color.FromArgb("#28213F"),Colors.Transparent,12),Stack(4,Text(title,compact?24:28,Ink,true),Text(subtitle,12,Muted))));
        root.Add(heading);
        var fieldStack=Stack(20,fields);
        if(demo)fieldStack.Children.Insert(0,Pill("DEMONSTRAÇÃO · SALVAMENTO DESATIVADO",Violet));
        var form=Card(fieldStack,compact?18:26);root.Add(new ScrollView{Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Never},0,1);
        var info=Text("",12,Coral);info.IsVisible=false;
        var feedback=Stack(12,info);bool saving=false;
        var saveButton=Button("Salvar "+(glyph=="calendar"?"agendamento":"lançamento"),async()=>
        {
            if(saving)return;saving=true;page.Saving=true;back.IsEnabled=cancel.IsEnabled=false;
            try{if(!await save())return;await Navigation.PopAsync();await Refresh();}
            catch(Exception e){info.IsVisible=true;info.Text=FriendlyError(e)+(e is NexusException?" Atualize a tela principal antes de repetir, pois a gravação pode ter sido concluída.":"");}
            finally{saving=false;page.Saving=false;back.IsEnabled=cancel.IsEnabled=true;}
        },primary:true);
        saveButton.IsEnabled=!demo;
        feedback.Add(Between(cancel,saveButton));root.Add(feedback,0,2);page.Content=root;
        await Navigation.PushAsync(page);
    }
    private void RenderConnection()
    {
        var url=Input("http://127.0.0.1:5080",Preferences.Default.Get("nexus-url","http://127.0.0.1:5080"));
        var token=Input("Cole sua chave de acesso");token.IsPassword=true;
        var databases=new Picker{Title="Primeiro, consulte os bancos",ItemDisplayBinding=new Binding(nameof(DatabaseChoice.Nome))};
        NexusClient? candidate=null;string? candidateUrl=null,candidateToken=null;
        var form=Stack(20,Row(12,Card(Icon("link",Violet,24),12,Color.FromArgb("#28213F"),Colors.Transparent,12),Stack(4,Text("Conectar ao Nexus",18,Ink,true),Text("Acesse seu banco na rede local.",11,Muted))),Rule(),
            Field("Endereço do servidor",url,"Neste computador: 127.0.0.1. No celular: endereço privado do computador Nexus."),
            Field("Chave de acesso",token,"A chave fica no armazenamento seguro deste dispositivo. Deixe em branco para usar a chave já salva."));
        form.Add(Button("Consultar bancos disponíveis",async()=>await Guard(async()=>
        {
            candidate?.Dispose();candidate=null;databases.ItemsSource=null;
            candidateToken=string.IsNullOrWhiteSpace(token.Text)?await SecureStorage.Default.GetAsync("nexus-token"):token.Text;candidateUrl=url.Text??"";
            var next=new NexusClient(candidateUrl,candidateToken??"");
            try{var banks=await next.Databases();databases.ItemsSource=banks;candidate=next;databases.Title=banks.Length==0?"Nenhum banco concedido à chave":"Selecione seu banco";
                status.Text=banks.Length==0?"Esta chave não possui bancos disponíveis. Confira as permissões no Nexus.":"Escolha seu banco abaixo para concluir a conexão.";}
            catch{next.Dispose();throw;}
        })));
        form.Add(Field("Banco de dados",databases));
        form.Add(Button("Salvar e conectar",async()=>await Guard(async()=>
        {
            if(candidate is null||databases.SelectedItem is not DatabaseChoice db)throw new ArgumentException("Consulte os bancos e selecione um para continuar.");
            if(url.Text!=candidateUrl||(!string.IsNullOrWhiteSpace(token.Text)&&token.Text!=candidateToken))throw new ArgumentException("O endereço ou a chave mudou. Consulte os bancos novamente.");
            var nextRepo=new AgendaRepository(candidate,db.Id);var nextAppointments=await nextRepo.Appointments();var nextEntries=await nextRepo.Entries();
            await SecureStorage.Default.SetAsync("nexus-token",candidateToken!);
            Preferences.Default.Set("nexus-url",candidateUrl!);Preferences.Default.Set("nexus-db",db.Id);Preferences.Default.Set("nexus-db-name",db.Nome);
            client?.Dispose();client=candidate;candidate=null;repository=nextRepo;appointments=nextAppointments;entries=nextEntries;demo=false;dataLoaded=true;connectionProblem=false;
            status.Text=$"Conectado · {db.Nome}";tab="Agenda";BuildShell();
        }),primary:true));
        if(repository is not null)form.Add(Button("Esquecer conexão neste dispositivo",async()=>
        {
            if(!await DisplayAlertAsync("Esquecer esta conexão?","A chave será removida deste dispositivo. Os dados no Nexus serão preservados.","Esquecer","Manter"))return;
            await Guard(async()=>{SecureStorage.Default.Remove("nexus-token");foreach(var key in new[]{"nexus-url","nexus-db","nexus-db-name"})Preferences.Default.Remove(key);
                client?.Dispose();client=null;repository=null;appointments=[];entries=[];demo=false;dataLoaded=false;connectionProblem=false;status.Text="Conexão removida deste dispositivo.";BuildShell();await Task.CompletedTask;});
        },danger:true));
        var guide=Stack(20,Text("Seus dados ficam com você.",22,Ink,true),Text("O Orbe organiza sua rotina. O Nexus cuida do seu banco de dados no computador.",13,Muted),Rule(),
            GuideStep("01","Prepare seu banco","No Nexus, crie um banco dedicado e as tabelas do Orbe usando o roteiro de configuração."),
            GuideStep("02","Ative a API","Gere uma chave com leitura e escrita apenas para o banco escolhido."),
            GuideStep("03","Conecte seus dispositivos","No celular, mantenha a mesma rede Wi-Fi do computador. O Nexus precisa estar em execução."),
            Card(Stack(8,Row(8,Icon("check",Mint,18),Text("Acesso local, sem conta na nuvem",12,Mint,true)),Text("A conexão utiliza a API de rede local do Nexus. O Orbe não envia seus dados a um servidor externo.",11,Muted)),16,CanvasColor),
            Button("Explorar demonstração",async()=>{LoadDemo();tab="Agenda";BuildShell();await Task.CompletedTask;},quiet:true));
        body.Add(Columns(Wide,Card(form,compact?18:26),Card(guide,compact?18:26),1.15,1));
    }
    private static View GuideStep(string number,string title,string description)=>new Grid
    {
        ColumnDefinitions=new(){new(new GridLength(30)),new(GridLength.Star)},ColumnSpacing=10,
        Children={Text(number,12,Violet,true),StepText(title,description)}
    };
    private static View StepText(string title,string description){var text=Stack(6,Text(title,13,Ink,true),Text(description,12,Muted));Grid.SetColumn(text,1);return text;}
}

internal sealed class EditorPage:ContentPage
{
    public bool Saving{get;set;}
    protected override bool OnBackButtonPressed()=>Saving||base.OnBackButtonPressed();
}
