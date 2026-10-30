using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbe.Server.Migrations
{
    /// <inheritdoc />
    public partial class ProfessionalServiceOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "EmployeeServices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Enabled",
                table: "EmployeeServices",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "EmployeeServices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PriceCents",
                table: "EmployeeServices",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Version",
                table: "EmployeeServices",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "EmployeeServices");

            migrationBuilder.DropColumn(
                name: "Enabled",
                table: "EmployeeServices");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "EmployeeServices");

            migrationBuilder.DropColumn(
                name: "PriceCents",
                table: "EmployeeServices");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "EmployeeServices");
        }
    }
}
