using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbe.Server.Migrations
{
    /// <inheritdoc />
    public partial class SharedConnection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApiBaseUrl",
                table: "Settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InstanceId",
                table: "Settings",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApiBaseUrl",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "InstanceId",
                table: "Settings");
        }
    }
}
