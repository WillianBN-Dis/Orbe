using Microsoft.EntityFrameworkCore;

namespace Orbe.Server;

public sealed class InboxNotification
{
 public Guid Id {get;set;}=Guid.NewGuid();
 public string UserId {get;set;}="";
 public string AppointmentId {get;set;}="";
 public string Kind {get;set;}="";
 public DateTimeOffset OccurrenceStart {get;set;}
 public DateTimeOffset DeliverAt {get;set;}
 public DateTimeOffset? ReadAt {get;set;}
 public DateTimeOffset? DeletedAt {get;set;}
}

public sealed class NotificationInbox(OrbeDb db,Actor actor,TimeProvider clock)
{
 // Materialize due events on the server, not on individual devices. Tombstones
 // and the unique occurrence key make polling/retries/deletion idempotent.
 public async Task Materialize()
 {
  var now=clock.GetUtcNow();var user=actor.Id;var owner=actor.Owner;
  await db.Database.ExecuteSqlInterpolatedAsync($"""
   INSERT INTO "Notifications" ("Id","UserId","AppointmentId","Kind","OccurrenceStart","DeliverAt")
   SELECT gen_random_uuid(),{user},a."Id",event.kind,a."Start",event.due
   FROM "Appointments" a LEFT JOIN "Employees" e ON e."Id"=a."EmployeeId"
   CROSS JOIN LATERAL (VALUES ('Booking',a."RecordedAt"),('Day',a."Start"-INTERVAL '24 hours'),('Hour',a."Start"-INTERVAL '1 hour')) event(kind,due)
   WHERE a."Status"='Confirmed' AND a."Kind"='Atendimento'
    AND ({owner} OR e."UserId"={user} OR a."ClientId"={user})
    AND event.due IS NOT NULL AND event.due<={now}
    AND (event.kind='Booking' OR (a."Start">{now} AND (a."RecordedAt" IS NULL OR a."RecordedAt"<=event.due)))
   ON CONFLICT ("UserId","AppointmentId","Kind","OccurrenceStart") DO NOTHING
   """);
 }
 private IQueryable<InboxNotification> Visible()=>db.Notifications.Where(n=>n.UserId==actor.Id&&n.DeletedAt==null&&n.DeliverAt<=clock.GetUtcNow()&&
  db.Appointments.Any(a=>a.Id==n.AppointmentId&&a.Status=="Confirmed"&&a.Kind=="Atendimento"&&a.Start==n.OccurrenceStart&&
   (actor.Owner||a.ClientId==actor.Id||db.Employees.Any(e=>e.Id==a.EmployeeId&&e.UserId==actor.Id))));
 public async Task<object> List(int offset,int limit)
 {
  if(offset<0||offset>100000||limit is <1 or >100)throw new ApiError(400,"Página inválida.");
  await Materialize();var visible=Visible();var unread=await visible.CountAsync(n=>n.ReadAt==null);var total=await visible.CountAsync();
  var items=await (from n in visible join a in db.Appointments on n.AppointmentId equals a.Id
   orderby n.DeliverAt descending,n.Id descending
   select new{n.Id,n.AppointmentId,n.Kind,n.OccurrenceStart,n.DeliverAt,n.ReadAt,subject=a.Title,
    person=actor.Client?db.Employees.Where(e=>e.Id==a.EmployeeId).Select(e=>e.DisplayName).FirstOrDefault():db.Users.Where(u=>u.Id==a.ClientId).Select(u=>u.FullName).FirstOrDefault()??a.LegacyClient})
   .Skip(offset).Take(limit).ToArrayAsync();
  return new{items,unreadCount=unread,total};
 }
 public async Task Read(Guid id)
 {
  // Repeated read/delete from another device is safe; someone else's ID never matches.
  if(!await db.Notifications.AnyAsync(n=>n.Id==id&&n.UserId==actor.Id))throw new ApiError(404,"Notificação não encontrada.");
  await Visible().Where(n=>n.Id==id&&n.ReadAt==null).ExecuteUpdateAsync(s=>s.SetProperty(n=>n.ReadAt,clock.GetUtcNow()));
 }
 public async Task Delete(Guid id)
 {
  var updated=await db.Notifications.Where(n=>n.Id==id&&n.UserId==actor.Id).ExecuteUpdateAsync(s=>s.SetProperty(n=>n.DeletedAt,clock.GetUtcNow()));
  if(updated==0)throw new ApiError(404,"Notificação não encontrada.");
 }
 public static void Map(WebApplication app)
 {
  var group=app.MapGroup("/api/v1/notifications").RequireAuthorization();
  group.MapGet("",async(NotificationInbox inbox,int offset=0,int limit=50)=>Results.Ok(await inbox.List(offset,limit)));
  group.MapPut("/{id:guid}/read",async(Guid id,NotificationInbox inbox)=>{await inbox.Read(id);return Results.NoContent();});
  group.MapDelete("/{id:guid}",async(Guid id,NotificationInbox inbox)=>{await inbox.Delete(id);return Results.NoContent();});
 }
}
