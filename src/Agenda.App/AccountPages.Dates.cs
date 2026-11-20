using static Agenda.App.OrbeUi;

namespace Agenda.App;

internal static partial class AccountPages
{
 private sealed class DateSelection
 {
  internal readonly VerticalStackLayout View=Stack(12);
  private readonly Picker mode=new(){ItemsSource=new[]{"Um dia","Vários dias","Intervalo"},SelectedIndex=0};
  private readonly DatePicker single=new(){Format="dd/MM/yyyy"};
  private readonly DatePicker first=new(){Format="dd/MM/yyyy"},last=new(){Format="dd/MM/yyyy"};
  private readonly HashSet<DateOnly> selected=[];
  private readonly List<CheckBox> weekdays=[];
  private readonly VerticalStackLayout calendar=Stack(10);
  private readonly Label preview=Text("",12,Muted);
  private DateOnly month;
  internal DateSelection(DateTime day)
  {
   first.Date=last.Date=day.Date;month=new(day.Year,day.Month,1);
   var range=Stack(12,Field("Data inicial",first),Field("Data final",last));
   var filters=new FlexLayout{Wrap=Microsoft.Maui.Layouts.FlexWrap.Wrap};
   for(var i=0;i<7;i++){
    var box=new CheckBox{IsChecked=true,Color=Violet};weekdays.Add(box);
    box.CheckedChanged+=(_,_)=>UpdatePreview();
    filters.Add(new HorizontalStackLayout{Spacing=3,Margin=new Thickness(0,0,12,4),Children={box,Text(Days[i][..3],12,Muted)}});
   }
   range.Add(Text("Incluir estes dias da semana",12,Muted,true));range.Add(filters);
   single.Date=day.Date;
   single.DateSelected+=(_,_)=>UpdatePreview();
   first.DateSelected+=(_,_)=>UpdatePreview();last.DateSelected+=(_,_)=>UpdatePreview();
   void UpdateMode(){single.IsVisible=mode.SelectedIndex==0;calendar.IsVisible=mode.SelectedIndex==1;range.IsVisible=mode.SelectedIndex==2;RenderMonth();UpdatePreview();}
   mode.SelectedIndexChanged+=(_,_)=>UpdateMode();
   View.Add(Field("Selecionar datas",mode));View.Add(single);View.Add(calendar);View.Add(range);
   View.Add(new ScrollView{Content=preview,MaximumHeightRequest=150});
   UpdateMode();
  }
  internal DateOnly[] Dates()
  {
   if(mode.SelectedIndex==0)return [DateOnly.FromDateTime(single.Date??DateTime.Today)];
   if(mode.SelectedIndex==1){
    if(selected.Count is <1 or >366)throw new ArgumentException("Selecione de 1 a 366 datas no calendário.");
    return selected.Order().ToArray();
   }
   var start=DateOnly.FromDateTime(first.Date??DateTime.Today);var end=DateOnly.FromDateTime(last.Date??DateTime.Today);
   var count=end.DayNumber-start.DayNumber+1;
   if(count is <1 or >366)throw new ArgumentException("Escolha um intervalo de até 366 dias com a data final após a inicial.");
   var dates=Enumerable.Range(0,count).Select(start.AddDays).Where(d=>weekdays[(int)d.DayOfWeek].IsChecked).ToArray();
   if(dates.Length==0)throw new ArgumentException("Marque ao menos um dia da semana.");
   return dates;
  }
  private void UpdatePreview()
  {
   try{var dates=Dates();preview.TextColor=Muted;preview.Text=$"{dates.Length} data(s) selecionada(s) — "+string.Join(", ",dates.Select(d=>d.ToString("dd/MM/yyyy")));}
   catch(ArgumentException e){preview.TextColor=Coral;preview.Text=e.Message;}
  }
  private void RenderMonth()
  {
   calendar.Children.Clear();
   Task Move(int delta){var next=month.AddMonths(delta);if(next.Year is >=1900 and <=2100){month=next;RenderMonth();}return Task.CompletedTask;}
   calendar.Add(Between(Text(month.ToString("MMMM yyyy",Agenda.Core.Finance.Brazilian),16,Ink,true),Row(2,Button("‹",()=>Move(-1),quiet:true),Button("›",()=>Move(1),quiet:true))));
   var grid=new Grid{ColumnSpacing=2,RowSpacing=4};
   for(var c=0;c<7;c++)grid.ColumnDefinitions.Add(new(GridLength.Star));
   for(var r=0;r<7;r++)grid.RowDefinitions.Add(new(GridLength.Auto));
   for(var c=0;c<7;c++){var label=Text(Days[c][..3],9,Dim);label.HorizontalTextAlignment=TextAlignment.Center;grid.Add(label,c,0);}
   for(var d=1;d<=DateTime.DaysInMonth(month.Year,month.Month);d++){
    var date=month.AddDays(d-1);var position=(int)month.DayOfWeek+d-1;
    var button=Button(d.ToString(),()=>{if(!selected.Remove(date))selected.Add(date);RenderMonth();UpdatePreview();return Task.CompletedTask;},quiet:true);
    button.Padding=0;button.FontSize=12;button.MinimumWidthRequest=0;button.MinimumHeightRequest=0;button.WidthRequest=40;button.HeightRequest=40;button.MaximumWidthRequest=44;button.HorizontalOptions=LayoutOptions.Center;button.VerticalOptions=LayoutOptions.Center;button.CornerRadius=10;
    button.BorderWidth=selected.Contains(date)?0:1;button.BorderColor=Line.WithAlpha(.5f);
    button.BackgroundColor=selected.Contains(date)?Violet:Colors.Transparent;button.TextColor=selected.Contains(date)?CanvasColor:Ink;
    SemanticProperties.SetDescription(button,date.ToString("dd/MM/yyyy")+(selected.Contains(date)?", selecionado":""));
    grid.Add(button,position%7,position/7+1);
   }
   grid.SizeChanged+=(_,_)=>{if(grid.Width<=0)return;var size=Math.Clamp((grid.Width-grid.ColumnSpacing*6)/7,24,44);foreach(var cell in grid.Children.OfType<Button>()){cell.WidthRequest=size;cell.HeightRequest=size;}};
   calendar.Add(grid);
  }
 }
}
