using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Orbe.Server.Migrations;

[DbContext(typeof(OrbeDb))]
[Migration("20261114120000_BackfillMembershipCatalogs")]
public sealed class BackfillMembershipCatalogs : Migration
{
 protected override void Up(MigrationBuilder migrationBuilder)=>migrationBuilder.Sql("""
  CREATE TEMP TABLE copied_catalog ON COMMIT DROP AS
  SELECT target."BusinessId", target."Id" AS employee, gen_random_uuid()::text AS service,
         COALESCE(link."Name", s."Name") AS name,
         COALESCE(link."DurationMinutes", s."DurationMinutes") AS duration,
         COALESCE(link."PriceCents", s."PriceCents") AS price,
         COALESCE(link."PriceFrom", s."PriceFrom") AS price_from,
         COALESCE(link."Enabled", s."Enabled") AS enabled,
         c."Name" AS category, c."NormalizedName" AS category_key
  FROM "Employees" target
  JOIN LATERAL (
   SELECT e."Id" FROM "Employees" e
   WHERE e."UserId"=target."UserId" AND e."Id"<>target."Id" AND e."Enabled"
     AND EXISTS(SELECT 1 FROM "EmployeeServices" l WHERE l."EmployeeId"=e."Id")
   ORDER BY e."BusinessId", e."Id" LIMIT 1
  ) source ON true
  JOIN "EmployeeServices" link ON link."EmployeeId"=source."Id"
  JOIN "Services" s ON s."Id"=link."ServiceId"
  LEFT JOIN "Categories" c ON c."Id"=link."CategoryId"
  WHERE target."BusinessId"<>1 AND target."Enabled"
   AND NOT EXISTS(SELECT 1 FROM "EmployeeServices" l WHERE l."EmployeeId"=target."Id");

  INSERT INTO "Categories" ("Id","BusinessId","Name","NormalizedName")
  SELECT gen_random_uuid()::text,"BusinessId",min(category),category_key
  FROM copied_catalog WHERE category_key IS NOT NULL GROUP BY "BusinessId",category_key
  ON CONFLICT ("BusinessId","NormalizedName") DO NOTHING;
  INSERT INTO "Services" ("Id","BusinessId","Name","DurationMinutes","PriceCents","PriceFrom","Enabled","Version")
  SELECT service,"BusinessId",name,duration,price,price_from,enabled,gen_random_uuid() FROM copied_catalog;
  INSERT INTO "EmployeeServices" ("EmployeeId","ServiceId","CategoryId")
  SELECT x.employee,x.service,c."Id" FROM copied_catalog x
  LEFT JOIN "Categories" c ON c."BusinessId"=x."BusinessId" AND c."NormalizedName"=x.category_key;
  """);

 // Copied offers may already be referenced by reservations.
 protected override void Down(MigrationBuilder migrationBuilder) { }
}
