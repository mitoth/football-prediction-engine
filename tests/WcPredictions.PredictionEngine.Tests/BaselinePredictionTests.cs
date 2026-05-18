using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using WcPredictions.Data;
using WcPredictions.PredictionEngine.Baseline;
using WcPredictions.PredictionEngine.Gateway;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace WcPredictions.PredictionEngine.Tests;

// §16 Phase 2 exit: a valid baseline (probs sum to 1, citations resolve) is
// stored and cached; a second request hits Redis with no second gateway call.
// Real Postgres + Redis via Testcontainers; the LLM Gateway stubbed by WireMock.
public class BaselinePredictionTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg =
        new PostgreSqlBuilder().WithImage("postgres:16").Build();
    private readonly RedisContainer _redis =
        new RedisBuilder().WithImage("redis:7").Build();
    private WireMockServer _wire = null!;

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();
        await _redis.StartAsync();
        _wire = WireMockServer.Start();
    }

    public async Task DisposeAsync()
    {
        _wire.Stop();
        await _pg.DisposeAsync();
        await _redis.DisposeAsync();
    }

    private WcDbContext NewDb() =>
        new(new DbContextOptionsBuilder<WcDbContext>()
            .UseNpgsql(_pg.GetConnectionString()).Options);

    private IDistributedCache NewCache() =>
        new RedisCache(Options.Create(new RedisCacheOptions
        {
            Configuration = _redis.GetConnectionString(),
        }));

    private BaselineService NewService(WcDbContext db) =>
        new(db,
            new LlmGatewayClient(new HttpClient { BaseAddress = new Uri(_wire.Url!) }),
            NewCache(),
            NullLogger<BaselineService>.Instance);

    private int PredictCalls() =>
        _wire.LogEntries.Count(e => e.RequestMessage.Path == "/predict");

    [Fact]
    public async Task Builds_valid_baseline_then_serves_from_cache()
    {
        await using (var db = NewDb())
            await db.Database.MigrateAsync();

        var leagueId = Guid.NewGuid();
        var homeId = Guid.NewGuid();
        var awayId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var realArticleId = Guid.NewGuid();

        await using (var db = NewDb())
        {
            db.Leagues.Add(new League { Id = leagueId, Name = "World Cup", CompetitionType = "national" });
            db.Teams.Add(new Team { Id = homeId, Name = "Brazil", IsNational = true });
            db.Teams.Add(new Team { Id = awayId, Name = "Argentina", IsNational = true });
            db.Matches.Add(new Match
            {
                Id = matchId,
                LeagueId = leagueId,
                HomeTeamId = homeId,
                AwayTeamId = awayId,
                KickoffUtc = DateTimeOffset.UtcNow.AddDays(3),
                Status = "NS",
            });
            db.Articles.Add(new Article
            {
                Id = realArticleId,
                Headline = "Brazil in form",
                Outlet = "BBC",
                Url = "https://news.test/a1",
                Snippet = "Brazil unbeaten in 6.",
                FetchedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        // Gateway returns un-normalized probs (sum 1.2) + one valid and one
        // invented citation; the engine must normalize and drop the invented id.
        _wire.Given(Request.Create().WithPath("/predict").UsingPost())
            .RespondWith(Response.Create().WithBodyAsJson(new
            {
                outcomeProbs = new { home = 0.5, draw = 0.3, away = 0.4 },
                predHome = 2,
                predAway = 1,
                why = "Brazil's home form is strong.",
                citations = new[] { realArticleId.ToString(), Guid.NewGuid().ToString() },
            }));

        // 1. Build
        BaselineDto dto;
        await using (var db = NewDb())
            dto = await NewService(db).BuildAsync(matchId, "manual", default);

        Assert.Equal(1, dto.Version);
        Assert.Equal(1.0, dto.Home + dto.Draw + dto.Away, 6);
        Assert.Equal(new[] { realArticleId.ToString() }, dto.Citations);
        Assert.Equal(1, PredictCalls());

        await using (var verify = NewDb())
        {
            var baseline = await verify.Baselines.SingleAsync(b => b.MatchId == matchId);
            Assert.Equal(1, baseline.Version);
            Assert.Equal(1, await verify.BaselineCitations.CountAsync(c => c.BaselineId == baseline.Id));
            var snap = await verify.PredictionSnapshots.SingleAsync(s => s.BaselineId == baseline.Id);
            Assert.Equal("baseline", snap.SourceKind);
            using var probs = JsonDocument.Parse(baseline.OutcomeProbs);
            var s = probs.RootElement.GetProperty("home").GetDouble()
                  + probs.RootElement.GetProperty("draw").GetDouble()
                  + probs.RootElement.GetProperty("away").GetDouble();
            Assert.Equal(1.0, s, 6);
        }

        // 2. Second request → Redis hit, NO second gateway call
        await using (var db = NewDb())
            await NewService(db).GetOrBuildAsync(matchId, default);
        Assert.Equal(1, PredictCalls());

        // 3. Explicit rebuild → version increments
        BaselineDto v2;
        await using (var db = NewDb())
            v2 = await NewService(db).BuildAsync(matchId, "manual", default);
        Assert.Equal(2, v2.Version);
        Assert.Equal(2, PredictCalls());
    }
}
