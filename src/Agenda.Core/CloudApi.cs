using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Agenda.Core;
public sealed class CloudException(string message,HttpStatusCode? status=null):Exception(message){public HttpStatusCode? Status {get;}=status;}
public sealed record CloudProfile(string Id,string FullName,string Email,string? Phone,DateOnly? BirthDate,bool EmailConfirmed,string[] Roles,string? EmployeeId)
{public bool Owner=>Roles.Contains("Owner");public bool Team=>Owner||Roles.Contains("Staff");}
public sealed record CloudSession(string AccessToken,DateTimeOffset ExpiresAt,CloudProfile Profile);
public sealed record CloudInfo(string Name,bool PublicBookingOpen,string TimeZoneId,int CancellationHours,bool EmailReady);
public sealed record CloudEmployee(string Id,string DisplayName,bool Enabled,Guid Version);
public sealed record CloudItem(string Id,string? EmployeeId,string? ClientId,string? ServiceId,string Title,string Kind,string Status,DateTimeOffset Start,DateTimeOffset End,string? LegacyClient,string? ClientName,string? Notes,long ExpectedCents,int DurationMinutes,Guid Version);
public sealed record CloudMoney(string Id,string? EmployeeId,string? AppointmentId,string Description,string Kind,DateOnly Date,long Cents,Guid Version);
public sealed class CloudApi:IDisposable
{
 public const string DefaultUrl="https://orbe-willianbn.discloud.dev";
 private readonly HttpClient http;
 private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
 public string BaseUrl {get;}
 public string? AccessToken {get;set;}
 public TimeZoneInfo BusinessZone {get;set;}=TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
 public CloudApi(string url,HttpMessageHandler? handler=null)
 {
  if(!Uri.TryCreate(url.Trim().TrimEnd('/')+"/",UriKind.Absolute,out var uri)||uri.AbsolutePath!="/"||uri.Query.Length>0||uri.Fragment.Length>0||uri.UserInfo.Length>0||!(uri.Scheme=="https"||uri.Scheme=="http"&&(uri.IsLoopback||uri.Host=="localhost")))throw new ArgumentException("Informe o endereço HTTPS do servidor Orbe, sem caminhos ou credenciais. HTTP é permitido somente em localhost para testes.");
  BaseUrl=uri.AbsoluteUri.TrimEnd('/');http=handler is null?new(new HttpClientHandler{AllowAutoRedirect=false}):new(handler);http.BaseAddress=uri;http.Timeout=TimeSpan.FromSeconds(25);
 }
 private async Task<T> Send<T>(HttpMethod method,string path,object? body=null)
 {
  using var request=new HttpRequestMessage(method,"api/v1/"+path.TrimStart('/'));
  if(AccessToken is not null)request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",AccessToken);
  if(body is not null)request.Content=JsonContent.Create(body,options:Json);
  try
  {
   using var response=await http.SendAsync(request);
   if(!response.IsSuccessStatusCode)
   {
    var message=response.StatusCode switch{HttpStatusCode.Unauthorized=>"Sua sessão terminou ou o usuário e a senha estão incorretos. Entre novamente.",HttpStatusCode.Forbidden=>"Sua conta não permite esta ação. Confira também a confirmação do e-mail.",HttpStatusCode.TooManyRequests=>"Muitas tentativas. Aguarde um pouco antes de continuar.",_=>"Não foi possível concluir. Atualize os dados antes de tentar novamente."};
    if(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict or HttpStatusCode.ServiceUnavailable or HttpStatusCode.NotFound)
    {try{var error=await response.Content.ReadFromJsonAsync<JsonElement>(Json);if(error.TryGetProperty("message",out var text)&&text.ValueKind==JsonValueKind.String&&text.GetString() is {Length:>0 and <700} detail)message=detail;}catch(JsonException){}}
    throw new CloudException(message,response.StatusCode);
   }
   if(response.StatusCode==HttpStatusCode.NoContent)return default!;
   return await response.Content.ReadFromJsonAsync<T>(Json)??throw new CloudException("O servidor retornou uma resposta inválida.");
  }
  catch(HttpRequestException){throw new CloudException("Não foi possível acessar o Orbe. Confira sua internet. Se estava salvando, atualize antes de repetir.");}
  catch(TaskCanceledException){throw new CloudException("O servidor demorou para responder. Atualize os dados para conferir se a ação foi concluída.");}
 }
 public Task<T> Get<T>(string path)=>Send<T>(HttpMethod.Get,path);
 public Task<T> Post<T>(string path,object body)=>Send<T>(HttpMethod.Post,path,body);
 public Task<T> Put<T>(string path,object body)=>Send<T>(HttpMethod.Put,path,body);
 public Task Delete(string path,object body)=>Send<JsonElement>(HttpMethod.Delete,path,body);
 public Task<CloudSession> Login(string login,string password)=>Post<CloudSession>("auth/token",new{login,password});
 public Task<CloudProfile> Me()=>Get<CloudProfile>("auth/me");
 public async Task ConfigureZone(){BusinessZone=TimeZoneInfo.FindSystemTimeZoneById((await Get<CloudInfo>("public/info")).TimeZoneId);}
 public DateTime Local(DateTimeOffset instant)=>TimeZoneInfo.ConvertTime(instant,BusinessZone).DateTime;
 public DateTimeOffset Instant(DateTime local)
 {local=DateTime.SpecifyKind(local,DateTimeKind.Unspecified);if(BusinessZone.IsInvalidTime(local)||BusinessZone.IsAmbiguousTime(local))throw new ArgumentException("O horário é inválido ou ambíguo no fuso do estabelecimento.");return new(TimeZoneInfo.ConvertTimeToUtc(local,BusinessZone));}
 public void Dispose()=>http.Dispose();
}
public interface IAgendaRepository
{
 Task<IReadOnlyList<Stored<Appointment>>> Appointments();
 Task<IReadOnlyList<Stored<MoneyEntry>>> Entries();
 Task Save(Appointment item,RowSnapshot? original=null);
 Task Save(MoneyEntry item);
 Task Delete(Stored<Appointment> item);
 Task Delete(Stored<MoneyEntry> item);
}
public sealed class CloudAgendaRepository(CloudApi api,string? employeeId):IAgendaRepository
{
 public string? EmployeeId {get;set;}=employeeId;
 private string Scope=>EmployeeId is null?"":"&employeeId="+Uri.EscapeDataString(EmployeeId);
 private static RowSnapshot Snapshot(string id,Guid version,string? clientId=null)=>new([JsonSerializer.SerializeToElement(id)],[JsonSerializer.SerializeToElement(version),JsonSerializer.SerializeToElement(clientId)]);
 private static Guid Version(RowSnapshot snapshot)=>snapshot.Valores[0].GetGuid();
 private async Task<List<T>> Pages<T>(string path)
 {var all=new List<T>();for(int offset=0;;offset+=200){var page=await api.Get<T[]>(path+"?offset="+offset+Scope);all.AddRange(page);if(page.Length<200)return all;}}
 public async Task<IReadOnlyList<Stored<Appointment>>> Appointments()=> (await Pages<CloudItem>("team/appointments")).Where(x=>x.Status=="Confirmed").Select(x=>new Stored<Appointment>(new(x.Id,x.Title,Enum.Parse<EventKind>(x.Kind),api.Local(x.Start),api.Local(x.End),x.ClientName??x.LegacyClient,x.Notes,x.ExpectedCents),Snapshot(x.Id,x.Version,x.ClientId))).ToArray();
 public async Task<IReadOnlyList<Stored<MoneyEntry>>> Entries()=> (await Pages<CloudMoney>("team/entries")).Select(x=>new Stored<MoneyEntry>(new(x.Id,x.Description,Enum.Parse<EntryKind>(x.Kind),x.Date,x.Cents,x.AppointmentId),Snapshot(x.Id,x.Version))).ToArray();
 public async Task Save(Appointment item,RowSnapshot? original=null)
 {
  item.Validate();if(EmployeeId is null)throw new ArgumentException("Selecione um profissional para salvar o agendamento.");
  if(item.Kind==EventKind.Atendimento)
  {
   var clientId=original is {Valores.Length:>1}&&original.Valores[1].ValueKind==JsonValueKind.String?original.Valores[1].GetString():null;
   var body=new{id=item.Id,employeeId=EmployeeId,item.Title,start=api.Instant(item.Start),end=api.Instant(item.End),clientId,legacyClient=item.Client,item.Notes,item.ExpectedCents,version=original is null?(Guid?)null:Version(original)};
   if(original is null)await api.Post<JsonElement>("team/appointments",body);else await api.Put<JsonElement>("team/appointments/"+Uri.EscapeDataString(item.Id),body);
  }
  else
  {
   var body=new{id=item.Id,employeeId=EmployeeId,item.Title,kind=item.Kind.ToString(),start=api.Instant(item.Start),end=api.Instant(item.End),item.Notes,version=original is null?(Guid?)null:Version(original)};
   if(original is null)await api.Post<JsonElement>("team/blocks",body);else await api.Put<JsonElement>("team/blocks/"+Uri.EscapeDataString(item.Id),body);
  }
 }
 public async Task Save(MoneyEntry item){item.Validate();await api.Post<JsonElement>("team/entries",new{id=item.Id,employeeId=EmployeeId,item.AppointmentId,description=item.Description,kind=item.Kind.ToString(),item.Date,item.Cents});}
 public Task Delete(Stored<Appointment> item)=>api.Delete("team/appointments/"+Uri.EscapeDataString(item.Value.Id),new{version=Version(item.Snapshot)});
 public Task Delete(Stored<MoneyEntry> item)=>api.Delete("team/entries/"+Uri.EscapeDataString(item.Value.Id),new{version=Version(item.Snapshot)});
}
