using Android.Content;
using Android.Content.PM;

namespace Agenda.App;

internal static class AndroidAppInstaller
{
 internal static bool Allowed()=>!OperatingSystem.IsAndroidVersionAtLeast(26)||global::Android.App.Application.Context.PackageManager!.CanRequestPackageInstalls();
 internal static void OpenPermission()
 {
  if(!OperatingSystem.IsAndroidVersionAtLeast(26))return;
  var context=global::Android.App.Application.Context;
  context.StartActivity(new Intent(global::Android.Provider.Settings.ActionManageUnknownAppSources,global::Android.Net.Uri.Parse("package:"+context.PackageName)).AddFlags(ActivityFlags.NewTask));
 }
 internal static void Validate(string file,int expectedBuild)
 {
  var context=global::Android.App.Application.Context;
  var info=context.PackageManager!.GetPackageArchiveInfo(file,(PackageInfoFlags)0);
  if(info is null||info.PackageName!=context.PackageName)throw new InvalidDataException("O pacote baixado não pertence ao Orbe.");
#pragma warning disable CS0618
  var build=OperatingSystem.IsAndroidVersionAtLeast(28)?info.LongVersionCode:info.VersionCode;
#pragma warning restore CS0618
  if(build!=expectedBuild)throw new InvalidDataException("O pacote baixado não corresponde ao ajuste anunciado.");
 }
}
