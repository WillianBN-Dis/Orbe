namespace Orbe.Cloud.Core;

public sealed record SharedConnection(string ApiUrl,Guid InstanceId,Guid Version);
public static class ConnectionAddress
{
 public static string Normalize(string? value,bool allowLoopback=false)
 {
  if(string.IsNullOrWhiteSpace(value)||!Uri.TryCreate(value.Trim(),UriKind.Absolute,out var uri)||uri.UserInfo.Length>0||uri.Query.Length>0||uri.Fragment.Length>0||uri.AbsolutePath!="/"||
   !(uri.Scheme=="https"&&uri.IsDefaultPort&&uri.Host.EndsWith(".discloud.dev",StringComparison.OrdinalIgnoreCase)||allowLoopback&&uri.IsLoopback&&(uri.Scheme is "http" or "https")))
   throw new ArgumentException("Informe o endereço HTTPS da aplicação Orbe na Discloud, sem caminhos ou credenciais.");
  return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
 }
}
