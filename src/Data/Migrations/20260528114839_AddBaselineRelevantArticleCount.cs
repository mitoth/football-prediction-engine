using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WcPredictions.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBaselineRelevantArticleCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RelevantArticleCount",
                table: "Baselines",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RelevantArticleCount",
                table: "Baselines");
        }
    }
}
