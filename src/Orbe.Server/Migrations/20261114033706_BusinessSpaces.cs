using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbe.Server.Migrations
{
    /// <inheritdoc />
    public partial class BusinessSpaces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Employees_UserId",
                table: "Employees");

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Settings",
                type: "text",
                nullable: false,
                defaultValue: "orbe");

            migrationBuilder.AddColumn<int>(
                name: "BusinessId",
                table: "Services",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "BusinessId",
                table: "Entries",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "CategoryId",
                table: "EmployeeServices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Administrator",
                table: "Employees",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "BusinessId",
                table: "Employees",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "BusinessId",
                table: "Appointments",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClientReservedAt",
                table: "Appointments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    BusinessId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    NormalizedName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.Sql("""
                SELECT setval(pg_get_serial_sequence('"Settings"', 'Id'), GREATEST(COALESCE(MAX("Id"), 1), 1)) FROM "Settings";
                UPDATE "Appointments" SET "ClientReservedAt" = "RecordedAt" WHERE "ClientId" IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Settings_Slug",
                table: "Settings",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeServices_CategoryId",
                table: "EmployeeServices",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_BusinessId_UserId",
                table: "Employees",
                columns: new[] { "BusinessId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_UserId",
                table: "Employees",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_BusinessId_NormalizedName",
                table: "Categories",
                columns: new[] { "BusinessId", "NormalizedName" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeServices_Categories_CategoryId",
                table: "EmployeeServices",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeServices_Categories_CategoryId",
                table: "EmployeeServices");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Settings_Slug",
                table: "Settings");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeServices_CategoryId",
                table: "EmployeeServices");

            migrationBuilder.DropIndex(
                name: "IX_Employees_BusinessId_UserId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_UserId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "BusinessId",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "BusinessId",
                table: "Entries");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "EmployeeServices");

            migrationBuilder.DropColumn(
                name: "Administrator",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "BusinessId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "BusinessId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "ClientReservedAt",
                table: "Appointments");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_UserId",
                table: "Employees",
                column: "UserId",
                unique: true);
        }
    }
}
