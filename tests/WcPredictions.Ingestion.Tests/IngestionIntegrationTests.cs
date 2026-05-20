using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;
using WcPredictions.Data;
using WcPredictions.Ingestion.ApiFootball;
using WcPredictions.Ingestion.News;
using WcPredictions.Ingestion.Sync;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace WcPredictions.Ingestion.Tests;

// §16 Phase 1 exit: after a sync, a known World Cup fixture + teams and ≥1
// article are queryable in Postgres. Real Postgres via Testcontainers;
// API-Football stubbed with WireMock; the news layer is curated football RSS,
// stubbed by WireMock serving an RSS 2.0 feed (the news layer is keyless).
public class IngestionIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg =
        new PostgreSqlBuilder().WithImage("postgres:16").Build();
    private WireMockServer _wire = null!;

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();
        _wire = WireMockServer.Start();
    }

    public async Task DisposeAsync()
    {
        _wire.Stop();
        await _pg.DisposeAsync();
    }

    private WcDbContext NewDb() =>
        new(new DbContextOptionsBuilder<WcDbContext>()
            .UseNpgsql(_pg.GetConnectionString()).Options);

    [Fact]
    public async Task Sync_ingests_world_cup_fixture_teams_and_news()
    {
        await using (var db = NewDb())
            await db.Database.MigrateAsync();

        _wire.Given(Request.Create().WithPath("/leagues").UsingGet())
            .RespondWith(Response.Create().WithBodyAsJson(new
            {
                response = new[]
                {
                    new { league = new { id = 1, name = "World Cup", type = "Cup" },
                          country = new { name = "World" } }
                }
            }));

        _wire.Given(Request.Create().WithPath("/teams").UsingGet())
            .RespondWith(Response.Create().WithBodyAsJson(new
            {
                response = new[]
                {
                    new { team = new { id = 6, name = "Brazil", national = true } },
                    new { team = new { id = 26, name = "Argentina", national = true } }
                }
            }));

        _wire.Given(Request.Create().WithPath("/fixtures").UsingGet())
            .RespondWith(Response.Create().WithBodyAsJson(new
            {
                response = new[]
                {
                    new
                    {
                        fixture = new { id = 9001, date = "2026-06-11T16:00:00+00:00",
                                         status = new { @short = "NS" } },
                        teams = new { home = new { id = 6, name = "Brazil" },
                                      away = new { id = 26, name = "Argentina" } }
                    }
                }
            }));

        const string rss = """
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0"><channel>
              <title>BBC Sport Football</title>
              <link>https://news.test/</link>
              <description>Football</description>
              <item>
                <title>WC 2026 preview: Brazil v Argentina</title>
                <link>https://news.test/wc1</link>
                <description>Brazil v Argentina opens the tournament.</description>
                <pubDate>Fri, 01 May 2026 00:00:00 GMT</pubDate>
              </item>
            </channel></rss>
            """;
        _wire.Given(Request.Create().WithPath("/football.rss").UsingGet())
            .RespondWith(Response.Create()
                .WithHeader("Content-Type", "application/rss+xml")
                .WithBody(rss));

        var afOpts = Options.Create(new ApiFootballOptions
        {
            BaseUrl = _wire.Url!,
            ApiKey = "test",
            Leagues = [new LeagueSeason { LeagueId = 1, Season = 2026 }],
        });
        var rssOpts = Options.Create(new RssOptions
        {
            Feeds = [new() { Url = $"{_wire.Url}/football.rss", Outlet = "BBC Sport" }],
        });

        await using (var db = NewDb())
        {
            await new FixtureSyncService(
                new ApiFootballClient(new HttpClient { BaseAddress = new Uri(_wire.Url!) }),
                db, afOpts, NullLogger<FixtureSyncService>.Instance).SyncAsync(default);
        }

        // First news sync: persists the seeded RSS item.
        await using (var db = NewDb())
        {
            await new NewsSyncService(
                new FootballRssClient(new HttpClient(), NullLogger<FootballRssClient>.Instance),
                db, rssOpts, NullLogger<NewsSyncService>.Instance).SyncAsync(default);
        }

        await using var verify = NewDb();

        var match = await verify.Matches
            .Include(m => m.HomeTeam).Include(m => m.AwayTeam).Include(m => m.League)
            .SingleAsync(m => m.ProviderFixtureId == "9001");
        Assert.Equal("Brazil", match.HomeTeam.Name);
        Assert.Equal("Argentina", match.AwayTeam.Name);
        Assert.Equal("national", match.League.CompetitionType);
        Assert.Equal(2, await verify.Teams.CountAsync());

        var article = await verify.Articles.SingleAsync();
        Assert.Equal("BBC Sport", article.Outlet);
        Assert.Equal("https://news.test/wc1", article.Url);
        Assert.Equal("WC 2026 preview: Brazil v Argentina", article.Headline);
        Assert.NotNull(article.PublishedAt);

        // Idempotency: second fixture + news sync must not duplicate.
        await using (var db = NewDb())
        {
            await new FixtureSyncService(
                new ApiFootballClient(new HttpClient { BaseAddress = new Uri(_wire.Url!) }),
                db, afOpts, NullLogger<FixtureSyncService>.Instance).SyncAsync(default);
        }
        await using (var db = NewDb())
        {
            await new NewsSyncService(
                new FootballRssClient(new HttpClient(), NullLogger<FootballRssClient>.Instance),
                db, rssOpts, NullLogger<NewsSyncService>.Instance).SyncAsync(default);
        }
        Assert.Equal(1, await verify.Matches.CountAsync());
        Assert.Equal(1, await verify.Articles.CountAsync());
    }
}
