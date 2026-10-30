using Microsoft.Maui.Controls.Shapes;

namespace Agenda.App;

internal static class OrbeUi
{
    public static Image BrandImage(double size)
    {
        var file="orbe.png";
#if WINDOWS
        file=System.IO.Path.Combine(AppContext.BaseDirectory,"orbe.scale-100.png");
#endif
        return new Image{Source=ImageSource.FromFile(file),WidthRequest=size,HeightRequest=size};
    }
    public static readonly Color CanvasColor = Color.FromArgb("#0D101B"), Sidebar = Color.FromArgb("#111521"), Surface = Color.FromArgb("#171C2B"), Raised = Color.FromArgb("#1D2435"), Line = Color.FromArgb("#2A3144"), Ink = Color.FromArgb("#F4F5FA"), Muted = Color.FromArgb("#9BA5BE"), Dim = Color.FromArgb("#68738D"), Violet = Color.FromArgb("#A795F9"), Mint = Color.FromArgb("#74DBB5"), Coral = Color.FromArgb("#F4A49D"), Blue = Color.FromArgb("#84B8F6");
    public static Label Text(string text, double size = 14, Color? color = null, bool bold = false) => new()
    { Text = text, FontSize = size, TextColor = color ?? Ink, FontFamily = bold ? "OpenSansSemibold" : "OpenSansRegular", LineBreakMode = LineBreakMode.WordWrap };
    public static VerticalStackLayout Stack(double spacing, params View[] views)
    { var s = new VerticalStackLayout { Spacing = spacing }; foreach (var v in views) s.Add(v); return s; }
    public static HorizontalStackLayout Row(double spacing, params View[] views)
    { var s = new HorizontalStackLayout { Spacing = spacing, VerticalOptions = LayoutOptions.Center }; foreach (var v in views) s.Add(v); return s; }
    public static Grid Between(View left, View right)
    { var g = new Grid { ColumnDefinitions = new() { new(GridLength.Star), new(GridLength.Auto) }, ColumnSpacing = 12 }; g.Add(left); g.Add(right, 1); return g; }
    public static Border Card(View content, double padding = 22, Color? background = null, Color? stroke = null, double radius = 18) => new()
    { Content = content, Padding = padding, BackgroundColor = background ?? Surface, Stroke = stroke ?? Line, StrokeThickness = 1, StrokeShape = new RoundRectangle { CornerRadius = radius } };
    public static View Rule() => new BoxView { HeightRequest = 1, Color = Line, HorizontalOptions = LayoutOptions.Fill };
    public static Border Pill(string label, Color color) => new()
    { BackgroundColor = color.WithAlpha(.10f), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 7 }, Padding = new Thickness(9, 5), Content = Text(label, 11, color, true), HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Center };
    public static Button Button(string text, Func<Task> action, bool primary = false, bool danger = false, bool quiet = false)
    {
        var b = new Button { Text = text, BackgroundColor = primary ? Violet : danger ? Color.FromArgb("#36262D") : quiet ? Colors.Transparent : Raised,
            TextColor = primary ? CanvasColor : danger ? Coral : Ink, FontFamily = "OpenSansSemibold", FontSize = 12,
            BorderColor = primary || quiet || danger ? Colors.Transparent : Line, BorderWidth = quiet ? 0 : 1,
            Padding = new Thickness(16, 11), MinimumHeightRequest = 44, CornerRadius = 10,
            MaximumWidthRequest = 320, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Center };
        b.Clicked += async (_, _) => { if (!b.IsEnabled) return; b.IsEnabled = false; try { await action(); } finally { b.IsEnabled = true; } };
        SemanticProperties.SetDescription(b, text);
        return b;
    }
    public static Grid Columns(bool wide, View first, View second, double firstWeight = 1, double secondWeight = 1, double gap = 20)
    {
        var grid = new Grid { ColumnSpacing = gap, RowSpacing = gap };
        if (wide) { grid.ColumnDefinitions.Add(new(new GridLength(firstWeight, GridUnitType.Star))); grid.ColumnDefinitions.Add(new(new GridLength(secondWeight, GridUnitType.Star))); grid.Add(first); grid.Add(second, 1); }
        else { grid.RowDefinitions.Add(new(GridLength.Auto)); grid.RowDefinitions.Add(new(GridLength.Auto)); grid.Add(first); grid.Add(second, 0, 1); }
        return grid;
    }
    public static GraphicsView Icon(string name, Color? color = null, double size = 22) => new()
    { Drawable = new LineIcon(name, color ?? Muted), WidthRequest = size, HeightRequest = size, VerticalOptions = LayoutOptions.Center, InputTransparent = true };
    public static Entry Input(string hint, string? value = null) => new()
    { Placeholder = hint, Text = value, TextColor = Ink, PlaceholderColor = Dim, FontSize = 14, BackgroundColor = Colors.Transparent, MinimumHeightRequest = 46 };
    public static View Field(string label, View control, string? helper = null)
    {
        SemanticProperties.SetDescription(control, label);
        var s = Stack(8, Text(label, 12, Muted, true), Card(control, 8, CanvasColor, Line, 10));
        if (helper is not null) s.Add(Text(helper, 11, Muted));
        return s;
    }
    public static Color KindColor(Agenda.Core.EventKind kind) => kind switch { Agenda.Core.EventKind.Atendimento => Violet, Agenda.Core.EventKind.Compromisso => Blue, Agenda.Core.EventKind.Folga => Mint, _ => Coral };
}

internal sealed class LineIcon(string name, Color color) : IDrawable
{
    public void Draw(ICanvas c, RectF r)
    {
        c.SaveState(); c.Scale(r.Width / 24, r.Height / 24); c.StrokeColor = color; c.StrokeSize = 1.6f; c.StrokeLineCap = LineCap.Round; c.StrokeLineJoin = LineJoin.Round;
        void Line(float x, float y, float a, float b) => c.DrawLine(x, y, a, b);
        if (name == "calendar") { c.DrawRoundedRectangle(3, 5, 18, 16, 3); Line(3,10,21,10); Line(8,3,8,7); Line(16,3,16,7); Line(7,14,9,14); Line(14,14,16,14); Line(7,18,9,18); }
        else if (name == "bell") { c.DrawArc(6,3,12,12,0,180,false,false); Line(6,9,6,17); Line(18,9,18,17); Line(4,18,20,18); c.DrawArc(9,18,6,5,180,360,false,false); }
        else if (name == "chart") { Line(4,3,4,21); Line(4,21,22,21); Line(8,16,12,11); Line(12,11,16,14); Line(16,14,21,6); }
        else if (name == "wallet") { c.DrawRoundedRectangle(3,5,18,15,3); c.DrawRoundedRectangle(14,10,8,6,2); c.DrawCircle(17,13,.5f); }
        else if (name == "link") { c.DrawRoundedRectangle(3,9,11,7,3); c.DrawRoundedRectangle(10,5,11,7,3); Line(9,12,15,9); }
        else if (name == "arrow") { Line(6,17,18,5); Line(9,5,18,5); Line(18,5,18,14); }
        else if (name == "down") { Line(6,7,18,19); Line(9,19,18,19); Line(18,19,18,10); }
        else if (name == "clock") { c.DrawCircle(12,12,9); Line(12,6,12,12); Line(12,12,16,14); }
        else if (name == "check") { Line(5,12,10,17); Line(10,17,20,6); }
        else if (name == "people") { c.DrawCircle(9,8,3); c.DrawArc(3,13,12,10,0,180,false,false); c.DrawCircle(17,8,2); c.DrawArc(14,14,7,8,0,180,false,false); }
        else if (name == "plus") { Line(12,5,12,19); Line(5,12,19,12); }
        else if (name == "refresh") { c.DrawArc(4,4,16,16,35,320,true,false); Line(19,3,20,9); Line(20,9,14,8); }
        else { c.DrawCircle(12,12,9); Line(12,7,12,13); c.DrawCircle(12,17,.5f); }
        c.RestoreState();
    }
}
