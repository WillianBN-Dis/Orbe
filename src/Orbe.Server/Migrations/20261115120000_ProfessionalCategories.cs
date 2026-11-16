using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Orbe.Server.Migrations;

[DbContext(typeof(OrbeDb))]
[Migration("20261115120000_ProfessionalCategories")]
public sealed class ProfessionalCategories : Migration
{
 protected override void Up(MigrationBuilder migrationBuilder)
 {
  migrationBuilder.AddColumn<string>(name:"EmployeeId",table:"Categories",type:"text",nullable:true);
  migrationBuilder.DropIndex(name:"IX_Categories_BusinessId_NormalizedName",table:"Categories");
  migrationBuilder.Sql("""
   CREATE TEMP TABLE category_scopes ON COMMIT DROP AS
   SELECT category, employee, CASE WHEN row_number() OVER (PARTITION BY category ORDER BY employee)=1
          THEN category ELSE replace(gen_random_uuid()::text,'-','') END AS scoped_id
   FROM (SELECT DISTINCT "CategoryId" AS category, "EmployeeId" AS employee
         FROM "EmployeeServices" WHERE "CategoryId" IS NOT NULL) AS links;

   UPDATE "Categories" c SET "EmployeeId"=m.employee
   FROM category_scopes m WHERE c."Id"=m.category AND m.category=m.scoped_id;

   INSERT INTO "Categories" ("Id","BusinessId","EmployeeId","Name","NormalizedName")
   SELECT m.scoped_id,e."BusinessId",m.employee,c."Name",c."NormalizedName"
   FROM category_scopes m JOIN "Categories" c ON c."Id"=m.category
   JOIN "Employees" e ON e."Id"=m.employee WHERE m.category<>m.scoped_id;

   UPDATE "EmployeeServices" l SET "CategoryId"=m.scoped_id
   FROM category_scopes m WHERE l."CategoryId"=m.category AND l."EmployeeId"=m.employee
   AND m.category<>m.scoped_id;
   """);
  migrationBuilder.CreateIndex(name:"IX_Categories_EmployeeId_NormalizedName",table:"Categories",
   columns:new[]{"EmployeeId","NormalizedName"},unique:true);
  migrationBuilder.AddForeignKey(name:"FK_Categories_Employees_EmployeeId",table:"Categories",column:"EmployeeId",
   principalTable:"Employees",principalColumn:"Id",onDelete:ReferentialAction.Cascade);
 }

 protected override void Down(MigrationBuilder migrationBuilder)
 {
  migrationBuilder.Sql("""
   CREATE TEMP TABLE category_merge ON COMMIT DROP AS
   SELECT "Id", min("Id") OVER (PARTITION BY "BusinessId","NormalizedName") AS canonical FROM "Categories";
   UPDATE "EmployeeServices" l SET "CategoryId"=m.canonical
   FROM category_merge m WHERE l."CategoryId"=m."Id" AND m."Id"<>m.canonical;
   DELETE FROM "Categories" c USING category_merge m WHERE c."Id"=m."Id" AND m."Id"<>m.canonical;
   """);
  migrationBuilder.DropForeignKey(name:"FK_Categories_Employees_EmployeeId",table:"Categories");
  migrationBuilder.DropIndex(name:"IX_Categories_EmployeeId_NormalizedName",table:"Categories");
  migrationBuilder.DropColumn(name:"EmployeeId",table:"Categories");
  migrationBuilder.CreateIndex(name:"IX_Categories_BusinessId_NormalizedName",table:"Categories",
   columns:new[]{"BusinessId","NormalizedName"},unique:true);
 }
}
