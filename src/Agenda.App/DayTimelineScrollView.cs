using Microsoft.Maui.Handlers;

namespace Agenda.App;

// Only the hourly timeline owns scrolling independently of the surrounding page.
internal sealed class DayTimelineScrollView : ScrollView { }

internal sealed class DayTimelineScrollHandler : ScrollViewHandler
{
#if ANDROID
    protected override Microsoft.Maui.Platform.MauiScrollView CreatePlatformView() =>
        new TimelineNativeScroll(new Android.Views.ContextThemeWrapper(Context, Microsoft.Maui.Resource.Style.scrollViewTheme))
            { FillViewport = true, ClipToOutline = true, NestedScrollingEnabled = false };

    private sealed class TimelineNativeScroll(Android.Content.Context context) :
        Microsoft.Maui.Platform.MauiScrollView(context, null!, Microsoft.Maui.Resource.Attribute.scrollViewStyle)
    {
        public override bool DispatchTouchEvent(Android.Views.MotionEvent? e)
        {
            if (e is null) return base.DispatchTouchEvent(e);
            Parent?.RequestDisallowInterceptTouchEvent(true);
            try { return base.DispatchTouchEvent(e); }
            finally
            {
                // Keep the complete gesture, even at the first/last hour. Release on cancel too.
                Parent?.RequestDisallowInterceptTouchEvent(
                    e.ActionMasked is not (Android.Views.MotionEventActions.Up or Android.Views.MotionEventActions.Cancel));
            }
        }
    }
#elif WINDOWS
    protected override void ConnectHandler(Microsoft.UI.Xaml.Controls.ScrollViewer platformView)
    {
        base.ConnectHandler(platformView);
        platformView.IsVerticalScrollChainingEnabled = false;
        platformView.IsHorizontalScrollChainingEnabled = false;
    }
#endif
}
