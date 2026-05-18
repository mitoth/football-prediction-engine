using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WcPredictions.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderUniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Teams_ProviderTeamId",
                table: "Teams",
                column: "ProviderTeamId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Matches_ProviderFixtureId",
                table: "Matches",
                column: "ProviderFixtureId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Leagues_ProviderLeagueId",
                table: "Leagues",
                column: "ProviderLeagueId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Articles_Url",
                table: "Articles",
                column: "Url",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Teams_ProviderTeamId",
                table: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_Matches_ProviderFixtureId",
                table: "Matches");

            migrationBuilder.DropIndex(
                name: "IX_Leagues_ProviderLeagueId",
                table: "Leagues");

            migrationBuilder.DropIndex(
                name: "IX_Articles_Url",
                table: "Articles");
        }
    }
}
