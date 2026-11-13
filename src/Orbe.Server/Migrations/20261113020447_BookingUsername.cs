using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbe.Server.Migrations
{
    /// <inheritdoc />
    public partial class BookingUsername : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BookingUsername",
                table: "AspNetUsers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_BookingUsername",
                table: "AspNetUsers",
                column: "BookingUsername",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_BookingUsername",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "BookingUsername",
                table: "AspNetUsers");
        }
    }
}
