using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using WcPredictions.Data;

namespace WcPredictions.Bff.Tests;

// §16 Phase 3 exit (backend half): anonymous match list → match detail →
// baseline card with citations that resolve to real articles. The real BFF is
// booted via WebApplicationFactory against a Testcontainers Postgres; no JWT is
// sent, proving the read path is anonymous (and /me is still 401, proving auth
// is still wired).
public class MatchReadEndpointsTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg =
        new PostgreSqlBuilder().WithImage("postgres:16").Build();
    private WebApplicationFactory<Program> _factory = null!;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b =>
            {
                b.UseEnvironment("Development");
                // Aspire's AddNpgsqlDbContext("wcdb") resolves this connection string.
                b.UseSetting("ConnectionStrings:wcdb", _pg.GetConnectionString());
            });
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _pg.DisposeAsync();
    }

    [Fact]
    public async Task Anonymous_match_list_and_detail_serve_baseline_with_resolving_citations()
    {
        var leagueId = Guid.NewGuid();
        var homeId = Guid.NewGuid();
        var awayId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var articleId = Guid.NewGuid();
        var baselineId = Guid.NewGuid();

        // Migrate explicitly (DbMigrator is a BackgroundService — racy with the
        // seed; MigrateAsync is idempotent so a double-apply is safe).
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WcDbContext>();
            await db.Database.MigrateAsync();

            db.Leagues.Add(new League { Id = leagueId, Name = "World Cup", CompetitionType = "national" });
            db.Teams.Add(new Team { Id = homeId, Name = "Brazil", IsNational = true });
            db.Teams.Add(new Team { Id = awayId, Name = "Argentina", IsNational = true });
            db.Matches.Add(new Match
            {
                Id = matchId,
                LeagueId = leagueId,
                HomeTeamId = homeId,
                AwayTeamId = awayId,
                KickoffUtc = DateTimeOffset.UtcNow.AddDays(5),
                Status = "NS",
            });
            db.Articles.Add(new Article
            {
                Id = articleId,
                Headline = "Brazil unbeaten in six",
                Outlet = "BBC",
                Url = "https://news.test/brazil-form",
                Snippet = "Tite's side arrive on a strong run.",
                FetchedAt = DateTimeOffset.UtcNow,
            });
            db.Baselines.Add(new Data.Baseline
            {
                Id = baselineId,
                MatchId = matchId,
                Version = 1,
                OutcomeProbs = """{"home":0.55,"draw":0.27,"away":0.18}""",
                PredHome = 2,
                PredAway = 1,
                Confidence = 0.55,
                WhyText = "Brazil's form and home-continent advantage.",
                RefreshTrigger = "manual",
                CreatedAt = DateTimeOffset.UtcNow,
            });
            db.BaselineCitations.Add(new BaselineCitation { BaselineId = baselineId, ArticleId = articleId });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient(); // anonymous — no Authorization header

        // /me still requires a JWT (auth wiring intact).
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/me")).StatusCode);

        // List: the seeded upcoming WC fixture is present, baseline flagged.
        var list = await client.GetFromJsonAsync<List<MatchListItem>>("/matches", Json);
        Assert.NotNull(list);
        var row = Assert.Single(list!, m => m.Id == matchId);
        Assert.Equal("World Cup", row.League);
        Assert.Equal("Brazil", row.HomeTeam);
        Assert.Equal("Argentina", row.AwayTeam);
        Assert.True(row.HasBaseline);

        // Detail: baseline probabilities sum to 1, scoreline + why present,
        // and the citation resolves to the real article we seeded.
        var detail = await client.GetFromJsonAsync<MatchDetail>($"/matches/{matchId}", Json);
        Assert.NotNull(detail);
        Assert.Equal("Brazil", detail!.HomeTeam);
        Assert.NotNull(detail.Baseline);
        var b = detail.Baseline!;
        Assert.Equal(1, b.Version);
        Assert.Equal(1.0, b.Home + b.Draw + b.Away, 6);
        Assert.Equal(2, b.PredHome);
        Assert.Equal(1, b.PredAway);
        Assert.False(string.IsNullOrWhiteSpace(b.Why));
        var cite = Assert.Single(b.Citations);
        Assert.Equal(articleId, cite.ArticleId);
        Assert.Equal("Brazil unbeaten in six", cite.Headline);
        Assert.Equal("https://news.test/brazil-form", cite.Url);

        // Unknown match → 404.
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/matches/{Guid.NewGuid()}")).StatusCode);
    }
}
