using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbe.Server.Migrations
{
    /// <inheritdoc />
    public partial class MembershipInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InvitationCode",
                table: "Settings",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Settings" SET "InvitationCode"=upper(replace(gen_random_uuid()::text,'-',''))::varchar(24);
                """);

            migrationBuilder.CreateTable(
                name: "MembershipApplications",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    BusinessId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MembershipApplications_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MembershipApplications_Settings_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Settings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Settings_InvitationCode",
                table: "Settings",
                column: "InvitationCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MembershipApplications_BusinessId_UserId",
                table: "MembershipApplications",
                columns: new[] { "BusinessId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MembershipApplications_UserId",
                table: "MembershipApplications",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MembershipApplications");

            migrationBuilder.DropIndex(
                name: "IX_Settings_InvitationCode",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "InvitationCode",
                table: "Settings");
        }
    }
}
