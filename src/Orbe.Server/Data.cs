using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Orbe.Cloud.Core;

namespace Orbe.Server;

public static class Roles { public const string Owner="Owner", Staff="Staff", Client="Client"; }
public sealed class AppUser : IdentityUser
{
 public string FullName {get;set;}="";
 public DateOnly? BirthDate {get;set;}
 public bool Active {get;set;}=true;
}
public sealed class Employee
{
 public string Id {get;set;}=Guid.NewGuid().ToString("N");
 public string UserId {get;set;}="";
 public AppUser User {get;set;}=null!;
 public string DisplayName {get;set;}="";
 public bool Enabled {get;set;}=true;
 public Guid Version {get;set;}=Guid.NewGuid();
}
public sealed class Service
{
 public string Id {get;set;}=Guid.NewGuid().ToString("N");
 public string Name {get;set;}="";
 public int DurationMinutes {get;set;}=60;
 public long PriceCents {get;set;}
 public bool Enabled {get;set;}=true;
 public Guid Version {get;set;}=Guid.NewGuid();
}
public sealed class EmployeeService
{
 public string EmployeeId {get;set;}=""; public string ServiceId {get;set;}="";
 // Null inherits the legacy catalog. Overrides isolate each professional without rewriting history.
 public string? Name {get;set;} public int? DurationMinutes {get;set;} public long? PriceCents {get;set;}
 public bool? Enabled {get;set;} public Guid Version {get;set;}=Guid.NewGuid();
}
public sealed class WeekWindow
{
 public long Id {get;set;}
 public string EmployeeId {get;set;}="";
 public int Day {get;set;}
 public int StartMinute {get;set;}
 public int EndMinute {get;set;}
}
public sealed class DayOverride
{
 public string EmployeeId {get;set;}="";
 public DateOnly Date {get;set;}
 public string WindowsJson {get;set;}="[]";
}
public sealed class ScheduleItem
{
 public string Id {get;set;}=Guid.NewGuid().ToString("N");
 public string? EmployeeId {get;set;}
 public string? ClientId {get;set;}
 public string? ServiceId {get;set;}
 public string Title {get;set;}="";
 public string Kind {get;set;}="Atendimento";
 public string Status {get;set;}="Confirmed";
 public DateTimeOffset Start {get;set;}
 public DateTimeOffset End {get;set;}
 public string? LegacyClient {get;set;}
 public string? Notes {get;set;}
 public long ExpectedCents {get;set;}
 public int DurationMinutes {get;set;}
 public Guid Version {get;set;}=Guid.NewGuid();
}
public sealed class FinancialEntry
{
 public string Id {get;set;}=Guid.NewGuid().ToString("N");
 public string? EmployeeId {get;set;}
 public string? AppointmentId {get;set;}
 public string? LegacyAppointmentId {get;set;}
 public ScheduleItem? Appointment {get;set;}
 public string Description {get;set;}="";
 public string Kind {get;set;}="Receita";
 public DateOnly Date {get;set;}
 public long Cents {get;set;}
 public Guid Version {get;set;}=Guid.NewGuid();
}
public sealed class BusinessSettings
{
 public Guid InstanceId {get;set;}=Guid.NewGuid();
 public string? ApiBaseUrl {get;set;}
 public int Id {get;set;}=1;
 public string Name {get;set;}="Orbe";
 public bool ImportCompleted {get;set;}
 public bool PublicBookingOpen {get;set;}
 public int SlotMinutes {get;set;}=15;
 public int LeadMinutes {get;set;}=120;
 public int HorizonDays {get;set;}=60;
 public int CancellationHours {get;set;}=24;
 public string TimeZoneId {get;set;}="America/Sao_Paulo";
 public Guid Version {get;set;}=Guid.NewGuid();
 public BookingPolicy Policy()=>new(SlotMinutes,LeadMinutes,HorizonDays,CancellationHours,TimeZoneId);
}
public sealed class LoginSession
{
 public string TokenHash {get;set;}="";
 public string UserId {get;set;}="";
 public string SecurityStamp {get;set;}="";
 public DateTimeOffset ExpiresAt {get;set;}
}
public sealed class BookingRequest
{
 public string UserId {get;set;}="";
 public string Key {get;set;}="";
 public string RequestHash {get;set;}="";
 public string AppointmentId {get;set;}="";
}
public sealed class ImportBatch
{
 public string Hash {get;set;}="";
 public DateTimeOffset ImportedAt {get;set;}
 public int Appointments {get;set;}
 public int Entries {get;set;}
}
public sealed class OrbeDb(DbContextOptions<OrbeDb> options) : IdentityDbContext<AppUser>(options), IDataProtectionKeyContext
{
 public DbSet<DataProtectionKey> DataProtectionKeys {get;set;}
 public DbSet<Employee> Employees=>Set<Employee>();
 public DbSet<Service> Services=>Set<Service>();
 public DbSet<EmployeeService> EmployeeServices=>Set<EmployeeService>();
 public DbSet<WeekWindow> WeekWindows=>Set<WeekWindow>();
 public DbSet<DayOverride> DayOverrides=>Set<DayOverride>();
 public DbSet<ScheduleItem> Appointments=>Set<ScheduleItem>();
 public DbSet<FinancialEntry> Entries=>Set<FinancialEntry>();
 public DbSet<BusinessSettings> Settings=>Set<BusinessSettings>();
 public DbSet<LoginSession> Sessions=>Set<LoginSession>();
 public DbSet<BookingRequest> BookingRequests=>Set<BookingRequest>();
 public DbSet<ImportBatch> Imports=>Set<ImportBatch>();
 protected override void OnModelCreating(ModelBuilder b)
 {
  base.OnModelCreating(b);
  b.Entity<BusinessSettings>().Property(x=>x.InstanceId).HasDefaultValueSql("gen_random_uuid()");
  b.Entity<Employee>().HasIndex(x=>x.UserId).IsUnique();
  b.Entity<Employee>().HasOne(x=>x.User).WithMany().HasForeignKey(x=>x.UserId).OnDelete(DeleteBehavior.Restrict);
  b.Entity<EmployeeService>().HasKey(x=>new{x.EmployeeId,x.ServiceId});
  b.Entity<EmployeeService>().Property(x=>x.Version).HasDefaultValueSql("gen_random_uuid()");
  b.Entity<EmployeeService>().HasOne<Employee>().WithMany().HasForeignKey(x=>x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
  b.Entity<EmployeeService>().HasOne<Service>().WithMany().HasForeignKey(x=>x.ServiceId).OnDelete(DeleteBehavior.Cascade);
  b.Entity<WeekWindow>().HasOne<Employee>().WithMany().HasForeignKey(x=>x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
  b.Entity<DayOverride>().HasKey(x=>new{x.EmployeeId,x.Date});
  b.Entity<DayOverride>().Property(x=>x.WindowsJson).HasColumnType("jsonb");
  b.Entity<DayOverride>().HasOne<Employee>().WithMany().HasForeignKey(x=>x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
  b.Entity<ScheduleItem>().HasOne<Employee>().WithMany().HasForeignKey(x=>x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
  b.Entity<ScheduleItem>().HasOne<AppUser>().WithMany().HasForeignKey(x=>x.ClientId).OnDelete(DeleteBehavior.Restrict);
  b.Entity<ScheduleItem>().HasOne<Service>().WithMany().HasForeignKey(x=>x.ServiceId).OnDelete(DeleteBehavior.Restrict);
  b.Entity<ScheduleItem>().HasIndex(x=>new{x.EmployeeId,x.Start,x.End});
  b.Entity<ScheduleItem>().HasIndex(x=>x.ClientId);
  b.Entity<FinancialEntry>().HasOne(x=>x.Appointment).WithMany().HasForeignKey(x=>x.AppointmentId).OnDelete(DeleteBehavior.Cascade);
  b.Entity<FinancialEntry>().HasOne<Employee>().WithMany().HasForeignKey(x=>x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
  b.Entity<LoginSession>().HasKey(x=>x.TokenHash);
  b.Entity<LoginSession>().HasOne<AppUser>().WithMany().HasForeignKey(x=>x.UserId).OnDelete(DeleteBehavior.Cascade);
  b.Entity<BookingRequest>().HasKey(x=>new{x.UserId,x.Key});
  b.Entity<ImportBatch>().HasKey(x=>x.Hash);
  foreach(var entity in b.Model.GetEntityTypes())
   if(entity.FindProperty("Version") is { } property) property.IsConcurrencyToken=true;
 }
}
