using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbe.Server.Migrations
{
    /// <inheritdoc />
    public partial class AndroidPush : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PushDevices",
                columns: table => new
                {
                    Token = table.Column<string>(type: "text", nullable: false),
                    SessionHash = table.Column<string>(type: "text", nullable: false),
                    RegisteredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushDevices", x => x.Token);
                    table.ForeignKey(
                        name: "FK_PushDevices_Sessions_SessionHash",
                        column: x => x.SessionHash,
                        principalTable: "Sessions",
                        principalColumn: "TokenHash",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PushReceipts",
                columns: table => new
                {
                    Token = table.Column<string>(type: "text", nullable: false),
                    NoticeId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushReceipts", x => new { x.Token, x.NoticeId });
                    table.ForeignKey(
                        name: "FK_PushReceipts_Notifications_NoticeId",
                        column: x => x.NoticeId,
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PushReceipts_PushDevices_Token",
                        column: x => x.Token,
                        principalTable: "PushDevices",
                        principalColumn: "Token",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PushDevices_SessionHash",
                table: "PushDevices",
                column: "SessionHash");

            migrationBuilder.CreateIndex(
                name: "IX_PushReceipts_NoticeId",
                table: "PushReceipts",
                column: "NoticeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PushReceipts");

            migrationBuilder.DropTable(
                name: "PushDevices");
        }
    }
}
