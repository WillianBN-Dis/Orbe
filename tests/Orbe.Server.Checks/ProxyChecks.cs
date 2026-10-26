using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orbe.Server;

internal static class ProxyChecks
{
 internal static async Task<bool> Verify(bool trusted,bool chain=false)
 {
  var options=new ForwardedHeadersOptions();
  var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Proxy:Addresses:0","10.1.2.3"}}).Build();
  ProxyTrust.Configure(options,config);
  var context=new DefaultHttpContext();context.Connection.RemoteIpAddress=IPAddress.Parse(trusted?"10.1.2.3":"192.0.2.10");context.Request.Scheme="http";
  context.Request.Headers["X-Forwarded-For"]=chain?"203.0.113.99, 198.51.100.20":"198.51.100.20";
  context.Request.Headers["X-Forwarded-Proto"]="https";
  var middleware=new ForwardedHeadersMiddleware(_=>Task.CompletedTask,NullLoggerFactory.Instance,Options.Create(options));
  await middleware.Invoke(context);
  return context.Connection.RemoteIpAddress!.ToString()==(trusted?"198.51.100.20":"192.0.2.10")&&context.Request.Scheme==(trusted?"https":"http");
 }
}
