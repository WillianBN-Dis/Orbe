using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbe.Server.Migrations
{
    /// <inheritdoc />
    public partial class SupportAndRecurring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SupportEmail",
                table: "Settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupportWhatsApp",
                table: "Settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SeriesId",
                table: "Appointments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_EmployeeId_SeriesId",
                table: "Appointments",
                columns: new[] { "EmployeeId", "SeriesId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appointments_EmployeeId_SeriesId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "SupportEmail",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "SupportWhatsApp",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "SeriesId",
                table: "Appointments");
        }
    }
}
