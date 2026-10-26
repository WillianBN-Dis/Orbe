using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Orbe.Server;

public static class ProxyTrust
{
 public static void Configure(ForwardedHeadersOptions options,IConfiguration configuration)
 {
  // Only the immediate, explicitly configured proxy can supply the client's IP and HTTPS scheme.
  // Keep the framework's loopback defaults; never clear the trust lists to accept all senders.
  options.ForwardedHeaders=ForwardedHeaders.XForwardedFor|ForwardedHeaders.XForwardedProto;
  options.ForwardLimit=1;
  foreach(var value in configuration.GetSection("Proxy:Addresses").Get<string[]>()??[])
  {
   if(!IPAddress.TryParse(value,out var address)||address.Equals(IPAddress.Any)||address.Equals(IPAddress.IPv6Any))
    throw new InvalidOperationException("Configure apenas endereços IP explícitos do proxy confiável.");
   options.KnownProxies.Add(address);
  }
 }
}
