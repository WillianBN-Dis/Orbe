using static Agenda.App.OrbeUi;

namespace Agenda.App;

internal static class ServiceDuration
{
    internal static string Format(int minutes) => minutes < 60 ? $"{minutes}min" :
        $"{minutes / 60}h" + (minutes % 60 == 0 ? "" : $" {minutes % 60}min");
}

internal sealed class ServiceDurationEditor : VerticalStackLayout
{
    internal readonly Picker Hours=new(){Title="Horas",ItemsSource=Enumerable.Range(0,25).Select(h=>$"{h} h").ToArray()};
    internal readonly Picker Remainder=new(){Title="Minutos",ItemsSource=Enumerable.Range(0,60).Select(m=>$"{m:D2} min").ToArray()};
    private readonly Label summary=Text("",12,Muted);
    internal int Minutes
    {
        get
        {
            var total=Hours.SelectedIndex*60+Remainder.SelectedIndex;
            if(Hours.SelectedIndex<0||Remainder.SelectedIndex<0||total is <1 or >1440)
                throw new ArgumentException("Escolha uma duração entre 1 minuto e 24 horas.");
            return total;
        }
    }
    internal ServiceDurationEditor(int minutes)
    {
        Spacing=10;Hours.SelectedIndex=minutes/60;Remainder.SelectedIndex=minutes%60;
        var fields=new Grid{ColumnDefinitions=new(){new(GridLength.Star),new(GridLength.Star)},ColumnSpacing=12};
        fields.Add(Field("Horas",Hours));fields.Add(Field("Minutos",Remainder),1);
        Add(Text("Duração do serviço",13,Muted));Add(fields);Add(summary);
        Hours.SelectedIndexChanged+=(_,_)=>Update();Remainder.SelectedIndexChanged+=(_,_)=>Update();Update();
    }
    private void Update()
    {
        try{summary.Text="Duração: "+ServiceDuration.Format(Minutes);summary.TextColor=Muted;}
        catch(ArgumentException e){summary.Text=e.Message;summary.TextColor=Coral;}
    }
}
