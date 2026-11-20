using Microsoft.Maui.Handlers;

namespace Agenda.App;

internal static class InstantInteraction
{
 internal static void Configure()=>ButtonHandler.Mapper.AppendToMapping("OrbeInstantTap",(handler,_)=>{
  handler.PlatformView.StateListAnimator=null;
  if(handler.PlatformView is Google.Android.Material.Button.MaterialButton button)
   button.RippleColor=Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
 });
}
