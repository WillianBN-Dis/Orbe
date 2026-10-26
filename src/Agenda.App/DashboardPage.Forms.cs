using Agenda.Core;
using static Agenda.App.OrbeUi;

namespace Agenda.App;

public sealed partial class DashboardPage
{
    private Task OpenAppointment(Stored<Appointment>? original=null)
    {
        if(repository is null||repository.EmployeeId is null)return NeedConnection();
        if(original is null&&people.Any(x=>x.Id==repository.EmployeeId&&!x.Enabled))return DisplayAlertAsync("Profissional inativo","Você pode consultar e ajustar o histórico. Reative o profissional antes de criar novos atendimentos.","Entendi");
        var a=original?.Value;var eventId=a?.Id??Guid.NewGuid().ToString("N");
        var title=Input("Ex.: Atendimento individual",a?.Title);var person=Input("Nome da pessoa ou cliente",a?.Client);
        var notes=new Editor{Text=a?.Notes,Placeholder="Algum detalhe importante para este compromisso?",AutoSize=EditorAutoSizeOption.TextChanges,MinimumHeightRequest=90,TextColor=Ink,PlaceholderColor=Dim,FontSize=14};
        var kind=new Picker{Title="Escolha o tipo",ItemsSource=Enum.GetNames<EventKind>(),SelectedIndex=(int)(a?.Kind??EventKind.Atendimento)};
        kind.IsEnabled=original is null;
        var from=new DatePicker{Date=a?.Start??Selected.ToDateTime(new(9,0)),Format="dd/MM/yyyy"};
        var fromTime=new TimePicker{Time=a?.Start.TimeOfDay??TimeSpan.FromHours(9),Format="HH:mm"};
        var until=new DatePicker{Date=a?.End??Selected.ToDateTime(new(10,0)),Format="dd/MM/yyyy"};
        var untilTime=new TimePicker{Time=a?.End.TimeOfDay??TimeSpan.FromHours(10),Format="HH:mm"};
        var amount=Input("0,00",((a?.ExpectedCents??0)/100m).ToString("F2",Finance.Brazilian));amount.Keyboard=Keyboard.Numeric;
        if(original is {Snapshot.Valores.Length:>1}&&original.Snapshot.Valores[1].ValueKind==System.Text.Json.JsonValueKind.String)person.IsReadOnly=true;
        void UpdateKind(){var service=kind.SelectedIndex==(int)EventKind.Atendimento;amount.IsEnabled=service;person.IsEnabled=service;}
        kind.SelectedIndexChanged+=(_,_)=>UpdateKind();UpdateKind();
        var fields=new View[]{Field("Título do agendamento",title),Columns(!compact,Field("Tipo de evento",kind),Field("Pessoa ou cliente · opcional",person)),Rule(),
            Text("Quando acontece?",18,Ink,true),Columns(!compact,Field("Data de início",from),Field("Horário de início",fromTime)),
            Columns(!compact,Field("Data de término",until),Field("Horário de término",untilTime)),Rule(),
            Field("Valor previsto · R$",amount,"Previsão para este evento. O recebimento é registrado separadamente."),Field("Observações · opcional",notes)};
        return OpenForm(a is null?"Novo agendamento":"Editar agendamento","Reserve um momento na sua rotina.","calendar",fields,async()=>
        {
            if(repository is null){await NeedConnection();return false;}
            var value=new Appointment(eventId,title.Text??"",(EventKind)kind.SelectedIndex,(from.Date??DateTime.Today).Date+(fromTime.Time??TimeSpan.Zero),
                (until.Date??DateTime.Today).Date+(untilTime.Time??TimeSpan.Zero),person.Text,notes.Text,Finance.ParseCents(amount.Text??"0"));value.Validate();
            if(appointments.Any(x=>x.Value.Id!=value.Id&&x.Value.Start<value.End&&x.Value.End>value.Start)&&
                !await DisplayAlertAsync("Já existe um evento neste horário","Você pode manter os dois compromissos. Deseja salvar mesmo assim?","Salvar mesmo assim","Revisar horário"))return false;
            await repository.Save(value,original?.Snapshot);return true;
        });
    }
    private Task OpenEntry(Appointment? appointment=null)
    {
        if(repository is null)return NeedConnection();
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
            if(repository is null){await NeedConnection();return false;}
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

        var form=Card(fieldStack,compact?18:26);root.Add(new ScrollView{Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Never},0,1);
        var info=Text("",12,Coral);info.IsVisible=false;
        var feedback=Stack(12,info);bool saving=false;
        var saveButton=Button("Salvar "+(glyph=="calendar"?"agendamento":"lançamento"),async()=>
        {
            if(saving)return;saving=true;page.Saving=true;back.IsEnabled=cancel.IsEnabled=false;
            try{if(!await save())return;await Navigation.PopAsync();await Refresh();}
            catch(Exception e){info.IsVisible=true;info.Text=FriendlyError(e)+(e is CloudException?" Atualize a tela principal antes de repetir, pois a gravação pode ter sido concluída.":"");}
            finally{saving=false;page.Saving=false;back.IsEnabled=cancel.IsEnabled=true;}
        },primary:true);

        feedback.Add(Between(cancel,saveButton));root.Add(feedback,0,2);page.Content=root;
        await Navigation.PushAsync(page);
    }
}

internal sealed class EditorPage:ContentPage
{
    public bool Saving{get;set;}
    protected override bool OnBackButtonPressed()=>Saving||base.OnBackButtonPressed();
}
