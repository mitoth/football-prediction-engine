using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WcPredictions.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchStage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Stage",
                table: "Matches",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Stage",
                table: "Matches");
        }
    }
}
