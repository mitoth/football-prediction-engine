using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WcPredictions.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Articles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Headline = table.Column<string>(type: "text", nullable: false),
                    Outlet = table.Column<string>(type: "text", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    Snippet = table.Column<string>(type: "text", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FetchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Articles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Leagues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CompetitionType = table.Column<string>(type: "text", nullable: false),
                    ProviderLeagueId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leagues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsNational = table.Column<bool>(type: "boolean", nullable: false),
                    ProviderTeamId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClerkUserId = table.Column<string>(type: "text", nullable: false),
                    Timezone = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExportRequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Matches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    HomeTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    AwayTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    KickoffUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ProviderFixtureId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Matches_Leagues_LeagueId",
                        column: x => x.LeagueId,
                        principalTable: "Leagues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Matches_Teams_AwayTeamId",
                        column: x => x.AwayTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Matches_Teams_HomeTeamId",
                        column: x => x.HomeTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Entitlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PassType = table.Column<string>(type: "text", nullable: false),
                    ScopeMatchDay = table.Column<DateOnly>(type: "date", nullable: true),
                    ScopeTournamentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ValidFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ValidTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StripeCheckoutId = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Entitlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Entitlements_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuotaLedger",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuotaDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SuccessCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotaLedger", x => new { x.UserId, x.QuotaDate });
                    table.ForeignKey(
                        name: "FK_QuotaLedger_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserLeagues",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLeagues", x => new { x.UserId, x.LeagueId });
                    table.ForeignKey(
                        name: "FK_UserLeagues_Leagues_LeagueId",
                        column: x => x.LeagueId,
                        principalTable: "Leagues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserLeagues_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Baselines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    OutcomeProbs = table.Column<string>(type: "jsonb", nullable: false),
                    PredHome = table.Column<int>(type: "integer", nullable: false),
                    PredAway = table.Column<int>(type: "integer", nullable: false),
                    Confidence = table.Column<double>(type: "double precision", nullable: false),
                    WhyText = table.Column<string>(type: "text", nullable: false),
                    RefreshTrigger = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Baselines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Baselines_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MatchResults",
                columns: table => new
                {
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    HomeGoals = table.Column<int>(type: "integer", nullable: false),
                    AwayGoals = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<string>(type: "text", nullable: false),
                    SettledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchResults", x => x.MatchId);
                    table.ForeignKey(
                        name: "FK_MatchResults_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BaselineCitations",
                columns: table => new
                {
                    BaselineId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArticleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BaselineCitations", x => new { x.BaselineId, x.ArticleId });
                    table.ForeignKey(
                        name: "FK_BaselineCitations_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BaselineCitations_Baselines_BaselineId",
                        column: x => x.BaselineId,
                        principalTable: "Baselines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Refinements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    BaselineVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    InputType = table.Column<string>(type: "text", nullable: false),
                    InputText = table.Column<string>(type: "text", nullable: true),
                    InputUrl = table.Column<string>(type: "text", nullable: true),
                    ExtractedText = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    QuotaCharged = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Refinements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Refinements_Baselines_BaselineVersionId",
                        column: x => x.BaselineVersionId,
                        principalTable: "Baselines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Refinements_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Refinements_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PredictionSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceKind = table.Column<string>(type: "text", nullable: false),
                    BaselineId = table.Column<Guid>(type: "uuid", nullable: true),
                    RefinementId = table.Column<Guid>(type: "uuid", nullable: true),
                    OutcomeProbs = table.Column<string>(type: "jsonb", nullable: false),
                    PredHome = table.Column<int>(type: "integer", nullable: false),
                    PredAway = table.Column<int>(type: "integer", nullable: false),
                    CapturedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PredictionSnapshots", x => x.Id);
                    table.CheckConstraint("ck_snapshot_polymorphic", "(\"BaselineId\" IS NOT NULL) <> (\"RefinementId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_PredictionSnapshots_Baselines_BaselineId",
                        column: x => x.BaselineId,
                        principalTable: "Baselines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PredictionSnapshots_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PredictionSnapshots_Refinements_RefinementId",
                        column: x => x.RefinementId,
                        principalTable: "Refinements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SnapshotScores",
                columns: table => new
                {
                    SnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchResultId = table.Column<Guid>(type: "uuid", nullable: false),
                    Brier = table.Column<double>(type: "double precision", nullable: false),
                    ScorelineDistance = table.Column<double>(type: "double precision", nullable: false),
                    BeatBaseline = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SnapshotScores", x => x.SnapshotId);
                    table.ForeignKey(
                        name: "FK_SnapshotScores_MatchResults_MatchResultId",
                        column: x => x.MatchResultId,
                        principalTable: "MatchResults",
                        principalColumn: "MatchId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SnapshotScores_PredictionSnapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "PredictionSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BaselineCitations_ArticleId",
                table: "BaselineCitations",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_Baselines_MatchId_Version",
                table: "Baselines",
                columns: new[] { "MatchId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Entitlements_UserId_Status",
                table: "Entitlements",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Matches_AwayTeamId",
                table: "Matches",
                column: "AwayTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_HomeTeamId",
                table: "Matches",
                column: "HomeTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_KickoffUtc",
                table: "Matches",
                column: "KickoffUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_LeagueId",
                table: "Matches",
                column: "LeagueId");

            migrationBuilder.CreateIndex(
                name: "IX_PredictionSnapshots_BaselineId",
                table: "PredictionSnapshots",
                column: "BaselineId");

            migrationBuilder.CreateIndex(
                name: "IX_PredictionSnapshots_MatchId",
                table: "PredictionSnapshots",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PredictionSnapshots_RefinementId",
                table: "PredictionSnapshots",
                column: "RefinementId");

            migrationBuilder.CreateIndex(
                name: "IX_Refinements_BaselineVersionId",
                table: "Refinements",
                column: "BaselineVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Refinements_MatchId",
                table: "Refinements",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Refinements_UserId_MatchId",
                table: "Refinements",
                columns: new[] { "UserId", "MatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_SnapshotScores_MatchResultId",
                table: "SnapshotScores",
                column: "MatchResultId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLeagues_LeagueId",
                table: "UserLeagues",
                column: "LeagueId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_ClerkUserId",
                table: "Users",
                column: "ClerkUserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BaselineCitations");

            migrationBuilder.DropTable(
                name: "Entitlements");

            migrationBuilder.DropTable(
                name: "QuotaLedger");

            migrationBuilder.DropTable(
                name: "SnapshotScores");

            migrationBuilder.DropTable(
                name: "UserLeagues");

            migrationBuilder.DropTable(
                name: "Articles");

            migrationBuilder.DropTable(
                name: "MatchResults");

            migrationBuilder.DropTable(
                name: "PredictionSnapshots");

            migrationBuilder.DropTable(
                name: "Refinements");

            migrationBuilder.DropTable(
                name: "Baselines");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Matches");

            migrationBuilder.DropTable(
                name: "Leagues");

            migrationBuilder.DropTable(
                name: "Teams");
        }
    }
}
