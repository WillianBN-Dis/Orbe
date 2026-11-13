using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Orbe.Server.Migrations;

[DbContext(typeof(OrbeDb))]
[Migration("20261113090000_RecurringReview")]
public sealed class RecurringReview : Migration
{
 protected override void Up(MigrationBuilder migrationBuilder)
 {
  migrationBuilder.Sql("""
   ALTER TABLE "Appointments" DROP CONSTRAINT "ValidAppointment";
   ALTER TABLE "Appointments" ADD CONSTRAINT "ValidAppointment"
    CHECK ("End">"Start" AND "ExpectedCents">=0 AND "Status" IN ('Confirmed','Cancelled','Pending'));
   """);
 }

 protected override void Down(MigrationBuilder migrationBuilder)
 {
  migrationBuilder.Sql("""
   UPDATE "Appointments" SET "Status"='Cancelled' WHERE "Status"='Pending';
   ALTER TABLE "Appointments" DROP CONSTRAINT "ValidAppointment";
   ALTER TABLE "Appointments" ADD CONSTRAINT "ValidAppointment"
    CHECK ("End">"Start" AND "ExpectedCents">=0 AND "Status" IN ('Confirmed','Cancelled'));
   """);
 }
}
