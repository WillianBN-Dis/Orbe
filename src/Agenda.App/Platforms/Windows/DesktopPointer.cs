using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Maui.Handlers;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Agenda.App;

internal static class DesktopPointer
{
    // WinUI exposes this cursor only as a protected property. Follow the
    // WindowsAppSDK InputCursor sample without replacing native MAUI controls.
    private static readonly PropertyInfo CursorProperty = typeof(UIElement)
        .GetProperty("ProtectedCursor", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static InputSystemCursor? hand, arrow;

    [DynamicDependency(DynamicallyAccessedMemberTypes.NonPublicProperties, typeof(UIElement))]
    internal static void Configure()
    {
        ViewHandler.ViewMapper.AppendToMapping("OrbeDesktopPointer", (handler, view) =>
        {
            if (handler.PlatformView is not FrameworkElement native ||
                view is not (IButton or IImageButton or IPicker or IDatePicker or ITimePicker or ICheckBox or ISwitch or ISlider)) return;
            void Update()
            {
                var enabled = native is not Control control || control.IsEnabled;
                CursorProperty.SetValue(native, enabled
                    ? hand ??= InputSystemCursor.Create(InputSystemCursorShape.Hand)
                    : arrow ??= InputSystemCursor.Create(InputSystemCursorShape.Arrow));
            }
            native.Loaded += (_, _) => Update();
            if (native is Control control) control.IsEnabledChanged += (_, _) => Update();
            if (native.IsLoaded) Update();
        });
    }
}
