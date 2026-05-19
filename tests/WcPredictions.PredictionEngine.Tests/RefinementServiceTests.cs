using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;
using WcPredictions.Data;
using WcPredictions.PredictionEngine.Baseline;
using WcPredictions.PredictionEngine.Gateway;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace WcPredictions.PredictionEngine.Tests;

// §16 Phase 4 (engine half): the refinement classification drives the quota.
// accepted=false ⇒ gibberish, relevant=false ⇒ off-topic — both echo the
// baseline unchanged; only accepted&relevant produces a normalized refined
// prediction with citations filtered to supplied articles.
public class RefinementServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg =
        new PostgreSqlBuilder().WithImage("postgres:16").Build();
    private WireMockServer _wire = null!;

    private Guid _matchId, _baselineId, _articleId;

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();
        _wire = WireMockServer.Start();
        await SeedAsync();
    }

    public async Task DisposeAsync()
    {
        _wire.Stop();
        await _pg.DisposeAsync();
    }

    private WcDbContext NewDb() =>
        new(new DbContextOptionsBuilder<WcDbContext>()
            .UseNpgsql(_pg.GetConnectionString()).Options);

    private RefinementService NewService(WcDbContext db) =>
        new(db,
            new LlmGatewayClient(new HttpClient { BaseAddress = new Uri(_wire.Url!) }),
            NullLogger<RefinementService>.Instance);

    private async Task SeedAsync()
    {
        await using var db = NewDb();
        await db.Database.MigrateAsync();

        var leagueId = Guid.NewGuid();
        var homeId = Guid.NewGuid();
        var awayId = Guid.NewGuid();
        _matchId = Guid.NewGuid();
        _baselineId = Guid.NewGuid();
        _articleId = Guid.NewGuid();

        db.Leagues.Add(new League { Id = leagueId, Name = "World Cup", CompetitionType = "national" });
        db.Teams.Add(new Team { Id = homeId, Name = "Brazil", IsNational = true });
        db.Teams.Add(new Team { Id = awayId, Name = "Argentina", IsNational = true });
        db.Matches.Add(new Match
        {
            Id = _matchId, LeagueId = leagueId, HomeTeamId = homeId, AwayTeamId = awayId,
            KickoffUtc = DateTimeOffset.UtcNow.AddDays(3), Status = "NS",
        });
        db.Articles.Add(new Article
        {
            Id = _articleId, Headline = "Brazil in form", Outlet = "BBC",
            Url = "https://news.test/a1", Snippet = "Unbeaten in 6.",
            FetchedAt = DateTimeOffset.UtcNow,
        });
        db.Baselines.Add(new Data.Baseline
        {
            Id = _baselineId, MatchId = _matchId, Version = 1,
            OutcomeProbs = """{"home":0.5,"draw":0.3,"away":0.2}""",
            PredHome = 2, PredAway = 1, Confidence = 0.5,
            WhyText = "Baseline reasoning.", RefreshTrigger = "manual",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private void StubRefine(object body) =>
        _wire.Given(Request.Create().WithPath("/refine").UsingPost())
            .RespondWith(Response.Create().WithBodyAsJson(body));

    [Fact]
    public async Task Success_normalizes_and_filters_citations()
    {
        StubRefine(new
        {
            accepted = true,
            relevant = true,
            rejectReason = "",
            outcomeProbs = new { home = 0.6, draw = 0.3, away = 0.3 }, // sum 1.2
            predHome = 3,
            predAway = 1,
            why = "User noted the keeper is injured.",
            citations = new[] { _articleId.ToString(), Guid.NewGuid().ToString() },
        });

        await using var db = NewDb();
        var r = await NewService(db).RefineAsync(_matchId, _baselineId, "keeper injured", default);

        Assert.Equal("success", r.Status);
        Assert.Equal(1.0, r.Home + r.Draw + r.Away, 6);
        Assert.Equal(3, r.PredHome);
        Assert.Equal(new[] { _articleId.ToString() }, r.Citations);
    }

    [Fact]
    public async Task Gibberish_is_rejected_and_echoes_baseline()
    {
        StubRefine(new
        {
            accepted = false,
            relevant = false,
            rejectReason = "gibberish",
            outcomeProbs = new { home = 0.5, draw = 0.3, away = 0.2 },
            predHome = 2,
            predAway = 1,
            why = "Not applied.",
            citations = Array.Empty<string>(),
        });

        await using var db = NewDb();
        var r = await NewService(db).RefineAsync(_matchId, _baselineId, "asdfqwer", default);

        Assert.Equal("rejected_gibberish", r.Status);
        Assert.Equal(0.5, r.Home, 6);   // baseline echoed
        Assert.Empty(r.Citations);
    }

    [Fact]
    public async Task Off_topic_is_rejected()
    {
        StubRefine(new
        {
            accepted = true,
            relevant = false,
            rejectReason = "off_topic",
            outcomeProbs = new { home = 0.5, draw = 0.3, away = 0.2 },
            predHome = 2,
            predAway = 1,
            why = "Not about this match.",
            citations = Array.Empty<string>(),
        });

        await using var db = NewDb();
        var r = await NewService(db).RefineAsync(_matchId, _baselineId, "best pasta recipe", default);

        Assert.Equal("off_topic", r.Status);
        Assert.Equal(2, r.PredHome); // baseline echoed
    }
}
