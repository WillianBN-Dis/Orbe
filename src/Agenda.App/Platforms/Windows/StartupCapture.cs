using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Agenda.App;

internal static class StartupCapture
{
 // Render only this application's login root, without capturing the user's desktop or other apps.
 internal static async Task Capture(Microsoft.UI.Xaml.FrameworkElement root,string path)
 {
  var bitmap=new RenderTargetBitmap();await bitmap.RenderAsync(root);
  if(bitmap.PixelWidth<=0||bitmap.PixelHeight<=0)throw new InvalidOperationException("Empty app render");
  var buffer=await bitmap.GetPixelsAsync();var bytes=new byte[checked((int)buffer.Length)];
  using(var reader=DataReader.FromBuffer(buffer))reader.ReadBytes(bytes);
  await File.WriteAllBytesAsync(path,[]);var file=await StorageFile.GetFileFromPathAsync(path);
  using var stream=await file.OpenAsync(FileAccessMode.ReadWrite);
  var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,stream);
  encoder.SetPixelData(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied,(uint)bitmap.PixelWidth,(uint)bitmap.PixelHeight,96,96,bytes);
  await encoder.FlushAsync();
 }
}
