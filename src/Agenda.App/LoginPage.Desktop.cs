#if WINDOWS
using Microsoft.Maui.Controls.Shapes;
using static Agenda.App.OrbeUi;

namespace Agenda.App;

public sealed partial class LoginPage
{
 private void BuildDesktopPresentation()
 {
  var ink=Color.FromArgb("#ECEBF8");
  var art=new Grid{HeightRequest=240};
  art.Add(new GraphicsView{Drawable=new LoginOrbitArt(),InputTransparent=true});
  var emblem=BrandImage(104);emblem.HorizontalOptions=emblem.VerticalOptions=LayoutOptions.Center;art.Add(emblem);
  var hero=Stack(24,
   Row(12,BrandImage(38),Text("orbe",30,ink,true)),
   Stack(12,Text("Seu tempo merece\num lugar melhor.",44,ink,true),Text("Organize sua rotina. Cuide das pessoas.\nAcompanhe cada conquista.",17,Color.FromArgb("#C1BBDD"))),
   art,
   Row(8,Pill("AGENDA",Violet),Pill("EQUIPE",Blue),Pill("FINANÇAS",Mint)),
   Text("Tudo conectado. No seu ritmo.",13,Color.FromArgb("#B5ADCF")));
  var heroPanel=Card(hero,40,stroke:Colors.Transparent,radius:20);
  heroPanel.Background=new LinearGradientBrush(new GradientStopCollection{
   new(Color.FromArgb("#30264F"),0),new(Color.FromArgb("#1B2037"),1)},new Point(0,0),new Point(1,1));
  heroPanel.Margin=12;

  // Remove the second, square WinUI frame inside the rounded form fields.
  foreach(var entry in new[]{user,password})
  {
   entry.FontSize=16;entry.MinimumHeightRequest=52;
   entry.HandlerChanged+=(_,_)=>{
    if(entry.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.TextBox box){
     box.BorderThickness=new Microsoft.UI.Xaml.Thickness(0);
     box.Resources["TextControlBorderBrushFocused"]=new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
     box.Resources["TextControlBorderBrushPointerOver"]=new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
     box.Resources["TextControlBorderBrush"]=new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }
   };
  }
  var reveal=Button("Mostrar",()=>{password.IsPassword=!password.IsPassword;return Task.CompletedTask;},quiet:true);
  reveal.FontSize=11;reveal.Padding=new Thickness(10,4);reveal.WidthRequest=72;
  reveal.Clicked+=(_,_)=>{reveal.Text=password.IsPassword?"Mostrar":"Ocultar";SemanticProperties.SetDescription(reveal,password.IsPassword?"Mostrar senha":"Ocultar senha");};
  var passwordRow=new Grid{ColumnDefinitions=new(){new(GridLength.Star),new(GridLength.Auto)}};passwordRow.Add(password);passwordRow.Add(reveal,1);
  var enter=Button("Entrar na minha conta  →",()=>Attempt(false),primary:true);
  var clientButton=Button("Reservar um horário como cliente  ↗",()=>Portal("/"));
  foreach(var button in new[]{enter,clientButton}){button.HorizontalOptions=LayoutOptions.Fill;button.MaximumWidthRequest=double.PositiveInfinity;button.MinimumHeightRequest=52;button.FontSize=14;}
  var recovery=Button("Esqueci minha senha",()=>Portal("/recuperar-senha"),quiet:true);recovery.FontSize=12;recovery.Padding=0;recovery.MinimumHeightRequest=32;recovery.HorizontalOptions=LayoutOptions.End;
  var feedback=Row(8,spinner,message);feedback.MinimumHeightRequest=24;feedback.IsVisible=false;
  void UpdateFeedback()=>feedback.IsVisible=spinner.IsVisible||!string.IsNullOrWhiteSpace(message.Text);
  message.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(Label.Text))UpdateFeedback();};
  spinner.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(ActivityIndicator.IsVisible))UpdateFeedback();};
  var compactBrand=Row(10,BrandImage(32),Text("orbe",26,Ink,true));
  var form=Stack(18,compactBrand,
   Stack(10,Text("BEM-VINDO AO SEU ESPAÇO",11,Violet,true),Text("Vamos começar?",34,Ink,true),Text("Entre com sua conta para acessar o Orbe.",15,Muted)),
   new BoxView{HeightRequest=8,Color=Colors.Transparent},
   Field("Usuário ou e-mail",user),Field("Senha",passwordRow),recovery,enter,feedback,
   Rule(),Stack(12,Text("Seu próximo atendimento começa aqui.",13,Muted),clientButton),
   Text("Acesso seguro para você e sua equipe.",11,Dim));
  form.MaximumWidthRequest=520;form.VerticalOptions=LayoutOptions.Center;
  var formPanel=new ContentView{Content=form,Padding=new Thickness(52,44)};
  var columns=new Grid{ColumnSpacing=0};
  var composition=Card(columns,0,Color.FromArgb("#141925"),Color.FromArgb("#34334A"),24);
  composition.MaximumWidthRequest=1360;composition.HorizontalOptions=LayoutOptions.Fill;composition.VerticalOptions=LayoutOptions.Center;
  var footer=Text("Orbe · Agenda e finanças   /   2.0.0 · "+AppInfo.Current.BuildString,11,Dim);footer.HorizontalTextAlignment=TextAlignment.Center;
  var group=Stack(18,composition,footer);group.VerticalOptions=LayoutOptions.Center;group.HorizontalOptions=LayoutOptions.Fill;
  var stage=new Grid{Padding=32};stage.Add(group);
  var wide=false;
  void ArrangeDesktop()
  {
   stage.MinimumHeightRequest=Math.Max(0,Height);var next=Width>=1200;compactBrand.IsVisible=!next;
   form.Spacing=Height<850?12:18;
   formPanel.Padding=next?new Thickness(52,Height<850?24:44):new Thickness(28,24);
   stage.Padding=Width>=1400?new Thickness(48):new Thickness(24);
   if(columns.Children.Count>0&&next==wide)return;wide=next;
   columns.Children.Clear();columns.ColumnDefinitions.Clear();columns.RowDefinitions.Clear();
   if(wide){columns.ColumnDefinitions.Add(new(new GridLength(1,GridUnitType.Star)));columns.ColumnDefinitions.Add(new(new GridLength(1.12,GridUnitType.Star)));columns.Add(heroPanel);columns.Add(formPanel,1);}
   else{columns.ColumnDefinitions.Add(new(GridLength.Star));columns.Add(formPanel);}
  }
  Content=new ScrollView{Content=stage};
  Background=new LinearGradientBrush(new GradientStopCollection{new(Color.FromArgb("#090D17"),0),new(Color.FromArgb("#19182B"),.5f),new(Color.FromArgb("#0B1120"),1)},new Point(0,0),new Point(1,1));
  SizeChanged+=(_,_)=>ArrangeDesktop();ArrangeDesktop();
#if DEBUG
  desktopLoginCheck=async()=>{
   await Attempt(false);
   if(message.Text!="Informe seu usuário e sua senha.")throw new InvalidOperationException("Empty login feedback missing");
   reveal.SendClicked();if(password.IsPassword||reveal.Text!="Ocultar")throw new InvalidOperationException("Password reveal failed");
   reveal.SendClicked();if(!password.IsPassword||reveal.Text!="Mostrar")throw new InvalidOperationException("Password hide failed");
   message.Text="";
   System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(),$"orbe-login-check-{Environment.ProcessId}.json"),"{\"passed\":true,\"emptyLogin\":true,\"passwordVisibility\":true}");
  };
#endif
 }
#if DEBUG
 private Func<Task>? desktopLoginCheck;
 internal Task QaDesktopLogin()=>desktopLoginCheck?.Invoke()??Task.CompletedTask;
#endif
}

internal sealed class LoginOrbitArt:IDrawable
{
 public void Draw(ICanvas c,RectF bounds)
 {
  c.SaveState();c.Translate(bounds.Center.X,bounds.Center.Y);var scale=Math.Min(bounds.Width/420,1);c.Scale(scale,scale);
  for(var i=8;i>0;i--){c.FillColor=Violet.WithAlpha(.018f);c.FillCircle(0,0,48+i*8);}
  c.StrokeSize=1;c.StrokeColor=Violet.WithAlpha(.13f);c.DrawCircle(0,0,108);
  c.SaveState();c.Rotate(-25);c.StrokeColor=Violet.WithAlpha(.65f);c.StrokeSize=1.5f;c.DrawEllipse(-177,-62,354,124);c.FillColor=Mint;c.FillCircle(172,14,5);c.RestoreState();
  c.SaveState();c.Rotate(32);c.StrokeColor=Blue.WithAlpha(.3f);c.DrawEllipse(-151,-87,302,174);c.FillColor=Violet;c.FillCircle(-136,-36,4);c.RestoreState();
  foreach(var p in new[]{new PointF(-177,-79),new PointF(137,-85),new PointF(-140,79),new PointF(167,81)}){c.FillColor=Violet.WithAlpha(.5f);c.FillCircle(p,2);}
  c.RestoreState();
 }
}
#endif
