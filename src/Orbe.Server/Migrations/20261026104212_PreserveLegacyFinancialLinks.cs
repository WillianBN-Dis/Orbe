using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbe.Server.Migrations
{
    /// <inheritdoc />
    public partial class PreserveLegacyFinancialLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LegacyAppointmentId",
                table: "Entries",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LegacyAppointmentId",
                table: "Entries");
        }
    }
}
