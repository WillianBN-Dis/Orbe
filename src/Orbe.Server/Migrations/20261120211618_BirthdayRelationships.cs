using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbe.Server.Migrations
{
    /// <inheritdoc />
    public partial class BirthdayRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClientRelationships",
                columns: table => new
                {
                    EmployeeId = table.Column<string>(type: "text", nullable: false),
                    ClientId = table.Column<string>(type: "text", nullable: false),
                    BusinessId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientRelationships", x => new { x.EmployeeId, x.ClientId });
                    table.ForeignKey(
                        name: "FK_ClientRelationships_AspNetUsers_ClientId",
                        column: x => x.ClientId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientRelationships_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientRelationships_ClientId",
                table: "ClientRelationships",
                column: "ClientId");
            // A persistent relationship survives cancellation or deletion of the event.
            migrationBuilder.Sql("""
                INSERT INTO "ClientRelationships" ("EmployeeId","ClientId","BusinessId")
                SELECT DISTINCT a."EmployeeId",a."ClientId",a."BusinessId" FROM "Appointments" a
                JOIN "Employees" e ON e."Id"=a."EmployeeId" AND e."BusinessId"=a."BusinessId"
                WHERE a."ClientId" IS NOT NULL AND a."Status"<>'Pending'
                ON CONFLICT DO NOTHING;
                CREATE FUNCTION orbe_link_customer() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF NEW."ClientId" IS NOT NULL AND NEW."EmployeeId" IS NOT NULL AND NEW."Status"='Confirmed' THEN
                    INSERT INTO "ClientRelationships" ("EmployeeId","ClientId","BusinessId")
                    SELECT NEW."EmployeeId",NEW."ClientId",NEW."BusinessId"
                    WHERE EXISTS (SELECT 1 FROM "Employees" e WHERE e."Id"=NEW."EmployeeId" AND e."BusinessId"=NEW."BusinessId")
                    ON CONFLICT DO NOTHING;
                  END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER orbe_link_customer_trigger AFTER INSERT OR UPDATE OF "ClientId","EmployeeId","Status"
                  ON "Appointments" FOR EACH ROW EXECUTE FUNCTION orbe_link_customer();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER orbe_link_customer_trigger ON \"Appointments\"; DROP FUNCTION orbe_link_customer();");
            migrationBuilder.DropTable(
                name: "ClientRelationships");
        }
    }
}
