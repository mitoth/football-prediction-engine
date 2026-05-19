using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WcPredictions.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRefinementOutput : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RefinedCitations",
                table: "Refinements",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RefinedPredAway",
                table: "Refinements",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RefinedPredHome",
                table: "Refinements",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefinedProbs",
                table: "Refinements",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefinedWhy",
                table: "Refinements",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RefinedCitations",
                table: "Refinements");

            migrationBuilder.DropColumn(
                name: "RefinedPredAway",
                table: "Refinements");

            migrationBuilder.DropColumn(
                name: "RefinedPredHome",
                table: "Refinements");

            migrationBuilder.DropColumn(
                name: "RefinedProbs",
                table: "Refinements");

            migrationBuilder.DropColumn(
                name: "RefinedWhy",
                table: "Refinements");
        }
    }
}
