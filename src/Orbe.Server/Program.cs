using System.Threading.RateLimiting;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orbe.Server;

var builder=WebApplication.CreateBuilder(args);
// Uploaded private configuration is outside wwwroot and never tracked in Git.
builder.Configuration.AddJsonFile("orbe.secrets.json",optional:true,reloadOnChange:false).AddEnvironmentVariables();
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics",LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command",LogLevel.Warning);
builder.Services.AddDbContext<OrbeDb>(o=>o.UseNpgsql(builder.Configuration.GetConnectionString("Orbe"),n=>n.CommandTimeout(15)));
builder.Services.AddIdentityCore<AppUser>(o=>{o.Password.RequiredLength=10;o.Password.RequireNonAlphanumeric=false;o.User.RequireUniqueEmail=true;o.Lockout.MaxFailedAccessAttempts=5;o.Lockout.DefaultLockoutTimeSpan=TimeSpan.FromMinutes(15);})
 .AddRoles<IdentityRole>().AddEntityFrameworkStores<OrbeDb>().AddDefaultTokenProviders();
var protection=builder.Services.AddDataProtection().SetApplicationName("Orbe.Cloud").PersistKeysToDbContext<OrbeDb>();
if(builder.Configuration["DataProtection:Certificate"] is {Length:>0} certificate)
 protection.ProtectKeysWithCertificate(X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(certificate),builder.Configuration["DataProtection:Password"],X509KeyStorageFlags.EphemeralKeySet));
else if(builder.Environment.IsProduction())throw new InvalidOperationException("Configure o certificado de proteção das chaves antes da implantação.");
builder.Services.AddAuthentication("Orbe").AddScheme<AuthenticationSchemeOptions,SessionAuth>("Orbe",null);
builder.Services.AddAuthorization();builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);builder.Services.AddScoped<Actor>();builder.Services.AddScoped<AuthService>();builder.Services.AddScoped<Scheduling>();builder.Services.AddScoped<Importing>();builder.Services.AddScoped<IEmailDelivery,SmtpEmail>();
builder.Services.AddAntiforgery(o=>{o.HeaderName="X-CSRF-TOKEN";o.Cookie.SameSite=SameSiteMode.Strict;o.Cookie.HttpOnly=true;});
builder.Services.AddRateLimiter(o=>
 {
  o.RejectionStatusCode=429;
  o.AddPolicy("auth",c=>RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new(){PermitLimit=builder.Configuration.GetValue("Limits:AuthPerMinute",20),Window=TimeSpan.FromMinutes(1),QueueLimit=0}));
  o.AddPolicy("account",c=>RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new(){PermitLimit=120,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));
  o.GlobalLimiter=PartitionedRateLimiter.Create<HttpContext,string>(c=>RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new(){PermitLimit=240,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));
 });
builder.WebHost.ConfigureKestrel(o=>o.Limits.MaxRequestBodySize=2*1024*1024);
var app=builder.Build();
app.Use(async(c,next)=>
 {
  c.Response.Headers.XContentTypeOptions="nosniff";c.Response.Headers["Referrer-Policy"]="no-referrer";
  c.Response.Headers["Content-Security-Policy"]="default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
  if(app.Environment.IsProduction())c.Response.Headers.StrictTransportSecurity="max-age=31536000";
  if(app.Configuration.GetValue<bool>("Database:SkipInitialize")&&c.Request.Path.StartsWithSegments("/api")){c.Response.StatusCode=503;await c.Response.WriteAsJsonAsync(new{message="O servidor está sendo preparado. As reservas continuam fechadas."});return;}
  try{await next();}
  catch(ApiError e){c.Response.StatusCode=e.Status;await c.Response.WriteAsJsonAsync(new{message=e.Message,details=e.Details});}
  catch(AntiforgeryValidationException){c.Response.StatusCode=400;await c.Response.WriteAsJsonAsync(new{message="Atualize a página e tente novamente."});}
  catch(ArgumentException){c.Response.StatusCode=400;await c.Response.WriteAsJsonAsync(new{message="Confira os valores informados."});}
  catch(DbUpdateConcurrencyException){c.Response.StatusCode=409;await c.Response.WriteAsJsonAsync(new{message="Os dados mudaram. Atualize antes de tentar novamente."});}
  catch(Exception e) when(e is DbUpdateException {InnerException:PostgresException {SqlState:"23P01" or "23505"}} or PostgresException {SqlState:"40001" or "40P01"})
  {c.Response.StatusCode=409;await c.Response.WriteAsJsonAsync(new{message="Este horário ou registro mudou. Atualize as opções e tente novamente."});}
  catch(Exception e) when(e is DbUpdateException {InnerException:PostgresException {SqlState:"23514" or "23503"}})
  {c.Response.StatusCode=400;await c.Response.WriteAsJsonAsync(new{message="Valores ou vínculos inválidos. Atualize os dados."});}
  catch(Exception e){app.Logger.LogError("Falha de requisição {Type}; referência {Trace}",e.GetType().Name,c.TraceIdentifier);c.Response.StatusCode=500;await c.Response.WriteAsJsonAsync(new{message="Não foi possível concluir agora. Tente novamente mais tarde.",reference=c.TraceIdentifier});}
 });
app.UseDefaultFiles();app.UseStaticFiles();app.UseRateLimiter();app.UseAuthentication();app.UseAuthorization();
app.Use(async(c,next)=>{if(c.Request.Path.StartsWithSegments("/api")&&c.Request.Method is not ("GET" or "HEAD" or "OPTIONS")&&c.Request.Cookies.ContainsKey(Secrets.Cookie)&&!c.Request.Headers.Authorization.ToString().StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase))await c.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(c);await next();});
app.MapGet("/health",async(OrbeDb db,HttpContext context)=>
{
 var ready=false;
 if(!app.Configuration.GetValue<bool>("Database:SkipInitialize"))
 {
  using var timeout=CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);timeout.CancelAfter(TimeSpan.FromSeconds(2));
  try{ready=await db.Database.CanConnectAsync(timeout.Token);}catch(OperationCanceledException){ }
 }
 return Results.Json(new{service="Orbe",status=ready?"ready":"unavailable",revision=app.Configuration["Deployment:Revision"]??"development"},statusCode:ready?200:503);
});
app.MapAuth();app.MapOrbe();app.MapImports();
app.MapFallback(async c=>{if(c.Request.Path.StartsWithSegments("/api")){c.Response.StatusCode=404;await c.Response.WriteAsJsonAsync(new{message="Recurso não encontrado."});}else{c.Response.ContentType="text/html; charset=utf-8";await c.Response.SendFileAsync(Path.Combine(app.Environment.WebRootPath,"index.html"));}});
if(!builder.Configuration.GetValue<bool>("Database:SkipInitialize"))await Bootstrap.Initialize(app.Services,app.Configuration);
app.Run();
public partial class Program { }
