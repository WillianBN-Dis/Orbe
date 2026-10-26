using Agenda.Core;
using static Agenda.App.OrbeUi;

namespace Agenda.App;

public sealed partial class DashboardPage
{
    private View Stat(string label, string value, string caption, string glyph, Color tint, bool featured=false)
    {
        var top=Between(Text(label,12,featured?Color.FromArgb("#CEC5EF"):Muted,true),Card(Icon(glyph,tint,18),8,featured?Color.FromArgb("#3B315A"):tint.WithAlpha(.10f),Colors.Transparent,10));
        return Card(Stack(14,top,Text(value,compact?25:28,featured?(tint==Coral?Coral:Ink):tint,true),Text(caption,11,featured?Color.FromArgb("#B7AFCF"):Dim)),20,
            featured?Color.FromArgb("#2C2345"):Surface,featured?Color.FromArgb("#4E3F70"):Line);
    }
    private Grid StatRow(params View[] views)
    {
        var g=new Grid { ColumnSpacing=14,RowSpacing=14 };
        if(compact) { var cards=new HorizontalStackLayout { Spacing=12 }; foreach(var v in views){v.WidthRequest=240;cards.Add(v);} g.Add(new ScrollView { Orientation=ScrollOrientation.Horizontal, HorizontalScrollBarVisibility=ScrollBarVisibility.Never,Content=cards }); }
        else { for(int i=0;i<views.Length;i++){g.ColumnDefinitions.Add(new(GridLength.Star));g.Add(views[i],i);} }
        return g;
    }
    private void RenderAgenda()
    {
        var today=appointments.Where(a=>a.Value.Intersects(Selected)).OrderBy(a=>a.Value.Start).ToArray();
        var daily=Finance.Summarize(entries.Select(e=>e.Value),Selected,Period.Dia);
        body.Add(StatRow(Stat("Agendamentos do dia",HasData?today.Length.ToString("D2"):"—","Atendimentos, compromissos e pausas","calendar",Violet),
            Stat("Valor previsto",HasData?Finance.Currency(today.Sum(a=>a.Value.ExpectedCents)):"—","Vinculado à agenda selecionada","wallet",Blue),
            Stat("Resultado do dia",HasData?Finance.Currency(daily.Profit):"—","Recebimentos menos despesas",daily.Profit<0?"down":"arrow",daily.Profit<0?Coral:Mint,true)));
        body.Add(Columns(Wide, BuildCalendar(), BuildTimeline(today), 1.3,1));
    }
    private View BuildCalendar()
    {
        var title=Text(TitleCase(calendarMonth.ToString("MMMM yyyy",Finance.Brazilian)),19,Ink,true);
        var previous=Button("‹",()=>MoveMonth(-1),quiet:true); previous.WidthRequest=40;
        var next=Button("›",()=>MoveMonth(1),quiet:true); next.WidthRequest=40;
        var today=Button("Hoje",async()=>{selected=DateOnly.FromDateTime(DateTime.Today);calendarMonth=new(selected.Year,selected.Month,1);await Render();},quiet:true);
        var s=Stack(20,Between(title,Row(0,previous,today,next)),Rule());
        var grid=new Grid { ColumnSpacing=4,RowSpacing=5 };
        for(int i=0;i<7;i++)grid.ColumnDefinitions.Add(new(GridLength.Star));
        grid.RowDefinitions.Add(new(new GridLength(30)));
        var weekdays=new[]{"SEG","TER","QUA","QUI","SEX","SÁB","DOM"};
        for(int i=0;i<7;i++){var l=Text(weekdays[i],9,Dim,true);l.HorizontalTextAlignment=TextAlignment.Center;grid.Add(l,i);}
        var start=calendarMonth.AddDays(-(((int)calendarMonth.DayOfWeek+6)%7));
        for(int row=0;row<6;row++)
        {
            grid.RowDefinitions.Add(new(new GridLength(compact?52:64)));
            for(int col=0;col<7;col++)
            {
                var day=start.AddDays(row*7+col); var isSelected=day==Selected; var current=day.Month==calendarMonth.Month;
                var events=appointments.Where(a=>a.Value.Intersects(day)).ToArray();
                var cell=Button(day.Day.ToString("D2"),async()=>{selected=day;if(!current)calendarMonth=new(day.Year,day.Month,1);await Render();},quiet:true);
                cell.FontSize=13;cell.Padding=0;cell.FontFamily=isSelected?"OpenSansSemibold":"OpenSansRegular";
                cell.BackgroundColor=isSelected?Violet:day==DateOnly.FromDateTime(DateTime.Today)?Raised:Colors.Transparent;
                cell.TextColor=isSelected?CanvasColor:current?Ink:Dim;
                cell.BorderWidth=day==DateOnly.FromDateTime(DateTime.Today)&&!isSelected?1:0;cell.BorderColor=Violet.WithAlpha(.5f);
                SemanticProperties.SetDescription(cell,$"{day:dd/MM/yyyy}, {events.Length} agendamentos");
                var layer=new Grid();layer.Add(cell);
                if(events.Length>0)
                {
                    var dots=new HorizontalStackLayout{Spacing=3,HorizontalOptions=LayoutOptions.Center,VerticalOptions=LayoutOptions.End,Margin=new Thickness(0,0,0,8),InputTransparent=true};
                    foreach(var type in events.Select(e=>e.Value.Kind).Distinct().Take(3)) dots.Add(new BoxView{WidthRequest=4,HeightRequest=4,CornerRadius=2,Color=isSelected?CanvasColor:KindColor(type)});
                    layer.Add(dots);
                }
                grid.Add(layer,col,row+1);
            }
        }
        s.Add(grid);s.Add(Rule());
        var legend=new FlexLayout{Wrap=Microsoft.Maui.Layouts.FlexWrap.Wrap,Direction=Microsoft.Maui.Layouts.FlexDirection.Row};
        foreach(var kind in Enum.GetValues<EventKind>()) {var r=Row(6,new BoxView{Color=KindColor(kind),HeightRequest=5,WidthRequest=5,CornerRadius=3,VerticalOptions=LayoutOptions.Center},Text(kind.ToString(),10,Muted));r.Margin=new Thickness(0,0,14,6);legend.Add(r);}
        s.Add(legend);
        s.Add(Text("Selecione um dia para ver seus agendamentos.",11,Dim));
        return Card(s,compact?14:22);
    }
    private Task MoveMonth(int amount)
    { var next=calendarMonth.AddMonths(amount);if(next.Year is <1900 or >2100)return Task.CompletedTask;calendarMonth=next;return Render(); }
    private View BuildTimeline(Stored<Appointment>[] today)
    {
        var heading=Stack(4,Text(Selected==DateOnly.FromDateTime(DateTime.Today)?"Hoje":"Agenda do dia",18,Ink,true),Text(TitleCase(Selected.ToString("dddd, dd 'de' MMMM",Finance.Brazilian)),11,Muted));
        var s=Stack(18,Between(heading,Pill($"{today.Length:D2} {(today.Length==1?"evento":"eventos")}",Violet)),Rule());
        var filter=new Picker{Title="Filtrar eventos",ItemsSource=new[]{"Todos os eventos","Atendimento","Compromisso","Folga","Outro"},SelectedIndex=kindFilter is null?0:(int)kindFilter+1,FontSize=12};
        filter.SelectedIndexChanged+=async(_,_)=>{kindFilter=filter.SelectedIndex==0?null:(EventKind)(filter.SelectedIndex-1);await Render();};
        s.Add(filter);
        var filtered=today.Where(a=>kindFilter is null||a.Value.Kind==kindFilter).ToArray();
        if(filtered.Length==0)
        {
            var empty=Stack(12,Icon("calendar",Violet,40),Text(!HasData?"Seu dia começa aqui":"Um espaço na sua rotina",17,Ink,true),Text(!HasData?"Atualize os dados para visualizar sua agenda.":"Nenhum evento para este dia ou filtro. Que tal planejar seu próximo compromisso?",12,Muted),Button("+  Agendar neste dia",()=>OpenAppointment(),quiet:true));
            empty.Padding=new Thickness(4,24);s.Add(empty);
        }
        foreach(var stored in filtered)
        {
            var a=stored.Value;var color=KindColor(a.Kind);
            var content=Stack(7,Between(Pill(a.Kind.ToString(),color),Button("···",()=>AppointmentActions(stored),quiet:true)),Text(a.Title,16,Ink,true),
                Row(6,Icon("clock",Dim,13),Text($"{a.Start:HH:mm} – {a.End:HH:mm}"+(DateOnly.FromDateTime(a.Start)!=DateOnly.FromDateTime(a.End)?$"  ·  até {a.End:dd/MM}":""),11,Muted)));
            if(!string.IsNullOrWhiteSpace(a.Client))content.Add(Text(a.Client,12,Muted));
            if(!string.IsNullOrWhiteSpace(a.Notes))content.Add(Text(a.Notes,11,Dim));
            if(a.ExpectedCents>0) content.Add(Text(Finance.Currency(a.ExpectedCents)+" previsto",12,color,true));
            var card=Card(content,16,CanvasColor,Line,12);
            var row=new Grid{ColumnDefinitions=new(){new(new GridLength(3)),new(GridLength.Star)},ColumnSpacing=10};
            row.Add(new BoxView{Color=color,CornerRadius=2,Margin=new Thickness(0,8)});row.Add(card,1);s.Add(row);
        }
        return Card(s,compact?16:22);
    }
    private async Task AppointmentActions(Stored<Appointment> stored)
    {
        var action=await DisplayActionSheetAsync(stored.Value.Title,"Voltar",null,"Editar agendamento","Lançar recebimento","Excluir agendamento");
        if(action=="Editar agendamento")await OpenAppointment(stored);
        else if(action=="Lançar recebimento")await OpenEntry(stored.Value);
        else if(action=="Excluir agendamento")
        {
            if(repository is null){await NeedConnection();return;}
            if(await DisplayAlertAsync("Excluir agendamento e lançamentos?",$"“{stored.Value.Title}” e todos os lançamentos financeiros vinculados a ele serão excluídos. O resultado financeiro será recalculado. Lançamentos sem vínculo com este agendamento serão mantidos.","Excluir tudo vinculado","Manter"))
                await Guard(async()=>{await repository.Delete(stored);await Reload();await Render();});
        }
    }
    private void RenderFinance()
    {
        var selector=new HorizontalStackLayout{Spacing=4};
        foreach(var (label,p) in new[]{("Dia",Period.Dia),("Mês",Period.Mes),("Ano",Period.Ano)})
        {var b=Button(label,async()=>{financialPeriod=p;await Render();},quiet:true);b.BackgroundColor=p==financialPeriod?Raised:Colors.Transparent;b.TextColor=p==financialPeriod?Ink:Muted;selector.Add(b);}
        var datePicker=new DatePicker{Date=Selected.ToDateTime(TimeOnly.MinValue),Format=financialPeriod==Period.Ano?"yyyy":financialPeriod==Period.Mes?"MMMM yyyy":"dd MMM yyyy",FontSize=12};
        datePicker.DateSelected+=async(_,_)=>{selected=DateOnly.FromDateTime(datePicker.Date??DateTime.Today);calendarMonth=new(selected.Year,selected.Month,1);await Render();};
        body.Add(Between(selector,datePicker));
        var summary=Finance.Summarize(entries.Select(e=>e.Value),Selected,financialPeriod);
        body.Add(StatRow(Stat("Recebimentos",HasData?Finance.Currency(summary.Income):"—","Entradas efetivamente recebidas","arrow",Mint),
            Stat("Despesas",HasData?Finance.Currency(summary.Expense):"—","Saídas registradas no período","down",Coral),
            Stat("Lucro do período",HasData?Finance.Currency(summary.Profit):"—","Seu resultado líquido","chart",summary.Profit<0?Coral:Violet,true)));
        var chartHeading=Stack(4,Text("Seu resultado, em perspectiva",18,Ink,true),Text(PeriodLabel(),11,Muted));
        var legend=new FlexLayout { WidthRequest=compact?-1:300, Wrap=Microsoft.Maui.Layouts.FlexWrap.Wrap, Direction=Microsoft.Maui.Layouts.FlexDirection.Row };
        foreach(var item in new[]{Legend("Lucro",Violet),Legend("Recebimentos",Mint),Legend("Despesas",Coral)}){item.Margin=new Thickness(0,0,12,4);legend.Add(item);}
        var chartStack=Stack(20,compact?Stack(12,chartHeading,legend):Between(chartHeading,legend),Rule());
        var drawing=new FinancialDrawing(summary.Buckets,HasData);
        var chart=new GraphicsView{Drawable=drawing,HeightRequest=270};
        SemanticProperties.SetDescription(chart,$"Gráfico do período. Recebimentos {Finance.Currency(summary.Income)}. Despesas {Finance.Currency(summary.Expense)}. Lucro {Finance.Currency(summary.Profit)}.");
        chartStack.Add(chart);
        chartStack.Add(Text("O lucro considera somente recebimentos e despesas lançados. Valores previstos não entram neste cálculo.",11,Dim));
        body.Add(Card(chartStack,compact?16:24));
        new Animation(p=>{drawing.Progress=(float)p;chart.Invalidate();},0,1).Commit(chart,"finance-reveal",16,700,Easing.CubicOut);
        var filtered=entries.Where(e=>InPeriod(e.Value.Date)).OrderByDescending(e=>e.Value.Date).ToArray();
        var transactionStack=Stack(18,Between(Stack(4,Text("Movimentações",18,Ink,true),Text("O detalhe de cada entrada e saída.",11,Muted)),Pill($"{filtered.Length} lançamentos",Violet)),Rule());
        if(filtered.Length==0)transactionStack.Add(Stack(10,Text("Nada por aqui, por enquanto.",15,Ink,true),Text("Seus recebimentos e despesas aparecerão neste período assim que forem registrados.",12,Muted)));
        foreach(var stored in filtered)
        {
            var e=stored.Value;var income=e.Kind==EntryKind.Receita;
            var left=Row(12,Card(Icon(income?"arrow":"down",income?Mint:Coral,18),10,(income?Mint:Coral).WithAlpha(.08f),Colors.Transparent,12),Stack(4,Text(e.Description,13,Ink,true),Text($"{e.Date:dd MMM yyyy}  ·  {(income?"Recebimento":"Despesa")}",10,Dim)));
            var right=Row(8,Text((income?"+ ":"− ")+Finance.Currency(e.Cents),13,income?Mint:Coral,true),Button("···",()=>EntryActions(stored),quiet:true));
            transactionStack.Add(compact?Stack(8,left,right):Between(left,right));transactionStack.Add(Rule());
        }
        body.Add(Card(transactionStack,compact?16:22));
    }
    private bool InPeriod(DateOnly date)=>financialPeriod switch{Period.Dia=>date==Selected,Period.Mes=>date.Year==Selected.Year&&date.Month==Selected.Month,_=>date.Year==Selected.Year};
    private string PeriodLabel()=>financialPeriod switch{Period.Dia=>Selected.ToString("dd 'de' MMMM 'de' yyyy",Finance.Brazilian),Period.Mes=>TitleCase(Selected.ToString("MMMM 'de' yyyy",Finance.Brazilian)),_=>Selected.Year.ToString()};
    private static View Legend(string name,Color color)=>Row(6,new BoxView{Color=color,HeightRequest=6,WidthRequest=6,CornerRadius=3,VerticalOptions=LayoutOptions.Center},Text(name,10,Muted));
    private static string TitleCase(string value)=>string.IsNullOrEmpty(value)?value:char.ToUpper(value[0],Finance.Brazilian)+value[1..];
    private async Task EntryActions(Stored<MoneyEntry> stored)
    {
        if(await DisplayActionSheetAsync(stored.Value.Description,"Voltar",null,"Excluir lançamento")!="Excluir lançamento")return;
        if(repository is null){await NeedConnection();return;}
        if(await DisplayAlertAsync("Excluir lançamento?","O lançamento será removido e o resultado financeiro será recalculado.","Excluir","Manter"))
            await Guard(async()=>{await repository.Delete(stored);await Reload();await Render();});
    }
}

internal sealed class FinancialDrawing(IReadOnlyList<FinancialBucket> buckets,bool connected):IDrawable
{
    public float Progress{get;set;}
    public void Draw(ICanvas c,RectF r)
    {
        if(r.Width<100)return;
        if(connected && buckets.Count==1){DrawDaily(c,r,buckets[0]);return;}
        var left=60f;var top=14f;var bottom=r.Height-34;var width=r.Width-left-12;var height=bottom-top;
        var max=Math.Max(100d,buckets.Max(b=>Math.Max(b.Income,b.Expense)))*1.15;
        var min=Math.Min(0d,buckets.Min(b=>(double)b.Profit))*1.15;
        float Y(double value)=>top+(float)((max-value)/(max-min))*height;
        var zero=Y(0);
        c.FontSize=10;c.FontColor=Dim;c.StrokeSize=1;
        for(int i=0;i<5;i++) {var y=top+height*i/4;c.StrokeColor=Line;c.DrawLine(left,y,r.Width,y);c.DrawString(connected?ShortMoney(max-(max-min)*i/4):"—",0,y-7,52,16,HorizontalAlignment.Left,VerticalAlignment.Center);}
        var n=buckets.Count;var slot=width/Math.Max(n,1);
        for(int i=0;i<n;i++)
        {
            if(n<13||i%5==0||i==n-1){c.FontColor=Dim;var labelX=Math.Clamp(left+slot*(i+.5f)-18,0,r.Width-36);c.DrawString(buckets[i].Label,labelX,bottom+13,36,20,HorizontalAlignment.Center,VerticalAlignment.Center);}
        }
        if(!connected){c.FontColor=Muted;c.FontSize=13;c.DrawString("Atualize os dados para acompanhar a evolução.",left,top+height/2-20,width,40,HorizontalAlignment.Center,VerticalAlignment.Center);return;}
        foreach(var series in new[]{0,1,2})
        {
            var line=new PathF();var area=new PathF();
            for(int i=0;i<n;i++)
            {
                var x=left+slot*(i+.5f);var value=series==0?buckets[i].Income:series==1?buckets[i].Expense:buckets[i].Profit;var y=Y(value*Progress);
                if(i==0){line.MoveTo(x,y);area.MoveTo(x,zero);area.LineTo(x,y);}else{line.LineTo(x,y);area.LineTo(x,y);}
            }
            area.LineTo(left+slot*(n-.5f),zero);area.Close();
            var color=series==0?Mint:series==1?Coral:Violet;
            c.SetFillPaint(new LinearGradientPaint{StartColor=color.WithAlpha(series==2?.17f:.02f),EndColor=color.WithAlpha(0),StartPoint=new Point(0,0),EndPoint=new Point(0,1)},new Rect(left,top,width,height));
            c.FillPath(area);c.StrokeColor=color;c.StrokeSize=series==2?2.8f:1.4f;c.DrawPath(line);
            if(n==1){c.FillColor=color;c.FillCircle(left+slot*.5f,Y((series==0?buckets[0].Income:series==1?buckets[0].Expense:buckets[0].Profit)*Progress),4);}
        }
        if(buckets.All(b=>b.Income==0&&b.Expense==0)) {c.FontColor=Muted;c.FontSize=12;c.DrawString("Nenhuma movimentação neste período",left,top+height/2-15,width,30,HorizontalAlignment.Center,VerticalAlignment.Center);}
    }
    private void DrawDaily(ICanvas c,RectF r,FinancialBucket day)
    {
        var rows=new[]{("Recebimentos",day.Income,Mint),("Despesas",day.Expense,Coral),("Lucro",day.Profit,day.Profit<0?Coral:Violet)};
        var max=Math.Max(1d,rows.Max(x=>Math.Abs((double)x.Item2)));
        for(var i=0;i<rows.Length;i++)
        {
            var (name,value,color)=rows[i];var y=12+i*76;
            c.FontSize=12;c.FontColor=Muted;c.DrawString(name,0,y,r.Width*.5f,22,HorizontalAlignment.Left,VerticalAlignment.Center);
            c.FontColor=color;c.DrawString(Finance.Currency(value),r.Width*.5f,y,r.Width*.5f,22,HorizontalAlignment.Right,VerticalAlignment.Center);
            c.FillColor=Line;c.FillRoundedRectangle(0,y+34,r.Width,10,5);
            c.FillColor=color;var w=(float)(Math.Abs((double)value)/max)*r.Width*Progress;if(w>0)c.FillRoundedRectangle(0,y+34,w,10,5);
        }
    }
    private static string ShortMoney(double cents){var reais=cents/100;return Math.Abs(reais)>=1000?"R$ "+(reais/1000).ToString("0.#",Finance.Brazilian)+" mil":"R$ "+reais.ToString("0",Finance.Brazilian);}
}
