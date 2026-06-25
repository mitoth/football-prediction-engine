using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WcPredictions.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChatMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "Refinements",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "AnonId",
                table: "Refinements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ip",
                table: "Refinements",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AnonQuotaLedger",
                columns: table => new
                {
                    AnonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ip = table.Column<string>(type: "text", nullable: false),
                    QuotaDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SuccessCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnonQuotaLedger", x => new { x.AnonId, x.Ip, x.QuotaDate });
                });

            migrationBuilder.CreateIndex(
                name: "IX_Refinements_AnonId_Ip_MatchId",
                table: "Refinements",
                columns: new[] { "AnonId", "Ip", "MatchId" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_refinement_identity",
                table: "Refinements",
                sql: "(\"UserId\" IS NOT NULL) <> (\"AnonId\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnonQuotaLedger");

            migrationBuilder.DropIndex(
                name: "IX_Refinements_AnonId_Ip_MatchId",
                table: "Refinements");

            migrationBuilder.DropCheckConstraint(
                name: "ck_refinement_identity",
                table: "Refinements");

            migrationBuilder.DropColumn(
                name: "AnonId",
                table: "Refinements");

            migrationBuilder.DropColumn(
                name: "Ip",
                table: "Refinements");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "Refinements",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
