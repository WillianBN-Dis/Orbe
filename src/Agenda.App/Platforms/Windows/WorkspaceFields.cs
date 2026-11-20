using Microsoft.Maui.Handlers;

namespace Agenda.App;

internal static class WorkspaceFields
{
 internal static void Configure()=>ViewHandler.ViewMapper.AppendToMapping("OrbeWorkspaceField",(handler,view)=>{
  if(view is not (Microsoft.Maui.IEntry or Microsoft.Maui.IEditor or Microsoft.Maui.IPicker or Microsoft.Maui.IDatePicker or Microsoft.Maui.ITimePicker))return;
  if(handler.PlatformView is not Microsoft.UI.Xaml.Controls.Control control)return;
  control.BorderThickness=new Microsoft.UI.Xaml.Thickness(0);
  control.Background=new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
  control.Padding=new Microsoft.UI.Xaml.Thickness(4);
  control.HorizontalContentAlignment=Microsoft.UI.Xaml.HorizontalAlignment.Stretch;
 });
}
