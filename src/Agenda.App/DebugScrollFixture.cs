#if DEBUG
using System.Text.Json;
using static Agenda.App.OrbeUi;

namespace Agenda.App;

internal sealed class DebugScrollFixture : ContentPage
{
    private readonly ScrollView outer;
    private readonly DayTimelineScrollView inner;
    private int taps;

    internal DebugScrollFixture()
    {
        BackgroundColor=CanvasColor;SafeAreaEdges=SafeAreaEdges.All;
        NavigationPage.SetHasNavigationBar(this,false);
        var hours=Stack(12);
        for(var i=0;i<24;i++)hours.Add(Button($"{i:D2}:00 — horário sintético",()=>{taps++;return Task.CompletedTask;}));
        inner=new DayTimelineScrollView{Content=hours,HeightRequest=180,VerticalScrollBarVisibility=ScrollBarVisibility.Always};
        outer=new ScrollView{Content=Stack(12,Text("QA rolagem independente",20),inner,new BoxView{HeightRequest=1400,Color=Line})};
        Content=outer;
    }

    internal async Task Verify()
    {
        string? error=null;
        try
        {
            await Task.Delay(700);
#if ANDROID
            var parent=(Microsoft.Maui.Platform.MauiScrollView)outer.Handler!.PlatformView!;
            var child=(Microsoft.Maui.Platform.MauiScrollView)inner.Handler!.PlatformView!;
            var density=child.Resources!.DisplayMetrics!.Density;
            async Task Gesture(bool inside,bool up,bool cancel=false)
            {
                var childLocation=new int[2];var parentLocation=new int[2];
                child.GetLocationOnScreen(childLocation);parent.GetLocationOnScreen(parentLocation);
                var x=parent.Width/2f;
                var top=inside?childLocation[1]-parentLocation[1]:childLocation[1]-parentLocation[1]+child.Height+25*density;
                var distance=inside?child.Height*.6f:90*density;
                var start=top+(up?distance:10*density);var end=top+(up?10*density:distance);
                var down=Android.OS.SystemClock.UptimeMillis();
                void Send(Android.Views.MotionEventActions action,float y,long elapsed)
                {
                    using var motion=Android.Views.MotionEvent.Obtain(down,down+elapsed,action,x,y,0)!;
                    parent.DispatchTouchEvent(motion);
                }
                Send(Android.Views.MotionEventActions.Down,start,0);
                for(var i=1;i<=12;i++){Send(Android.Views.MotionEventActions.Move,start+(end-start)*i/12,i*20);await Task.Delay(20);}
                Send(cancel?Android.Views.MotionEventActions.Cancel:Android.Views.MotionEventActions.Up,end,260);
                await Task.Delay(700);
            }
            await Gesture(true,true);
            if(child.ScrollY<=0||parent.ScrollY!=0)throw new Exception("Inner gesture moved parent or failed to scroll hours");
            child.ScrollTo(0,child.GetChildAt(0)!.Height);await Task.Delay(100);
            await Gesture(true,true);
            if(parent.ScrollY!=0)throw new Exception("Bottom boundary handed gesture to page");
            child.ScrollTo(0,0);await Task.Delay(100);
            await Gesture(true,false);
            if(parent.ScrollY!=0)throw new Exception("Top boundary handed gesture to page");
            await Gesture(true,true,true);
            await Gesture(false,true);
            if(parent.ScrollY<=0)throw new Exception("Outside gesture did not scroll page after cancel");
            parent.ScrollTo(0,0);child.ScrollTo(0,0);await Task.Delay(100);
            var first=((VerticalStackLayout)inner.Content).Children.OfType<Button>().First();
            var target=(Android.Views.View)first.Handler!.PlatformView!;
            var point=new int[2];var origin=new int[2];target.GetLocationOnScreen(point);parent.GetLocationOnScreen(origin);
            var now=Android.OS.SystemClock.UptimeMillis();
            using(var down=Android.Views.MotionEvent.Obtain(now,now,Android.Views.MotionEventActions.Down,point[0]-origin[0]+target.Width/2f,point[1]-origin[1]+target.Height/2f,0))parent.DispatchTouchEvent(down!);
            using(var up=Android.Views.MotionEvent.Obtain(now,now+80,Android.Views.MotionEventActions.Up,point[0]-origin[0]+target.Width/2f,point[1]-origin[1]+target.Height/2f,0))parent.DispatchTouchEvent(up!);
            await Task.Delay(100);if(taps!=1)throw new Exception("Timeline tap was lost");
#elif WINDOWS
            var child=(Microsoft.UI.Xaml.Controls.ScrollViewer)inner.Handler!.PlatformView!;
            if(child.IsVerticalScrollChainingEnabled||child.IsHorizontalScrollChainingEnabled)throw new Exception("Timeline chaining enabled");
            await inner.ScrollToAsync(0,300,false);await Task.Delay(150);
            if(inner.ScrollY<=0||outer.ScrollY!=0)throw new Exception("Inner scroll moved parent");
            await outer.ScrollToAsync(0,150,false);await Task.Delay(150);
            if(outer.ScrollY<=0)throw new Exception("Outer page cannot scroll independently");
#endif
        }
        catch(Exception e){error=e.ToString();}
        var report=JsonSerializer.Serialize(new{passed=error is null,error,taps});
        System.IO.File.WriteAllText(System.IO.Path.Combine(FileSystem.AppDataDirectory,"scroll-check.json"),report);
#if WINDOWS
        System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(),$"orbe-scroll-{Environment.ProcessId}.json"),report);
        Environment.Exit(error is null?0:1);
#endif
    }
}
#endif
