using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;
using WcPredictions.Bff;
using WcPredictions.Data;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace WcPredictions.Bff.Tests;

// §16 Phase 4 exit (backend): anonymous refine is 401; a free user gets 3
// successful refinements then the 4th is blocked; gibberish and a dead URL
// cost no credit; editing the chip is a free re-run; removing it reverts.
// Real BFF via WebApplicationFactory + Testcontainers Postgres; the engine and
// URL fetcher are WireMock; the Clerk JWT is minted with the dev signing key.
public class RefineEndpointsTests : IAsyncLifetime
{
    private const string DevKey = "phase0-dev-only-signing-key-change-me!";
    private readonly PostgreSqlContainer _pg =
        new PostgreSqlBuilder().WithImage("postgres:16").Build();
    private WireMockServer _engine = null!;
    private WireMockServer _fetcher = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web);

    private Guid _matchId, _baselineId, _articleId;

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();
        _engine = WireMockServer.Start();
        _fetcher = WireMockServer.Start();
        StubFetcher("ok", "Extracted article text about the match.");

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:wcdb", _pg.GetConnectionString());
            b.UseSetting("Clerk:DevSigningKey", DevKey);
            b.ConfigureServices(s =>
            {
                // Point the typed clients at WireMock (overrides the
                // service-discovery base address registered in Program.cs).
                s.AddHttpClient<PredictionEngineClient>(c => c.BaseAddress = new Uri(_engine.Url!));
                s.AddHttpClient<UrlFetcherClient>(c => c.BaseAddress = new Uri(_fetcher.Url!));
            });
        });

        await SeedAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        _engine.Stop();
        _fetcher.Stop();
        await _pg.DisposeAsync();
    }

    private async Task SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WcDbContext>();
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
            WhyText = "Baseline.", RefreshTrigger = "manual",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private string Jwt(string tier)
    {
        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(DevKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            claims: new[]
            {
                new Claim("sub", "user_clerk_123"),
                new Claim("tier", tier),
            },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private HttpClient Client(string? tier)
    {
        var c = _factory.CreateClient();
        if (tier is not null)
            c.DefaultRequestHeaders.Authorization = new("Bearer", Jwt(tier));
        return c;
    }

    private void StubEngine(string status)
    {
        _engine.Reset();
        _engine.Given(Request.Create().WithPath("/refine").UsingPost())
            .RespondWith(Response.Create().WithBodyAsJson(new
            {
                status,
                home = 0.6, draw = 0.25, away = 0.15,
                predHome = 3, predAway = 1,
                why = status == "success" ? "Refined with the user's note." : "Not applied.",
                citations = status == "success" ? new[] { _articleId.ToString() } : Array.Empty<string>(),
            }));
    }

    private void StubFetcher(string status, string? text)
    {
        _fetcher.Reset();
        _fetcher.Given(Request.Create().WithPath("/fetch").UsingPost())
            .RespondWith(Response.Create().WithBodyAsJson(new { status, text }));
    }

    private async Task<(HttpStatusCode code, JsonElement body)> Send(
        HttpClient c, HttpMethod m, object? payload)
    {
        using var req = new HttpRequestMessage(m, $"/matches/{_matchId}/refine");
        if (payload is not null) req.Content = JsonContent.Create(payload);
        var resp = await c.SendAsync(req);
        var body = resp.Content.Headers.ContentLength is > 0
            ? (await resp.Content.ReadFromJsonAsync<JsonElement>(J))
            : default;
        return (resp.StatusCode, body);
    }

    [Fact]
    public async Task Full_refinement_hook()
    {
        var text = new { inputType = "text", text = "Star striker injured" };
        var url = new { inputType = "url", url = "https://news.test/report" };

        // 1. Anonymous → 401.
        var anon = Client(null);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Send(anon, HttpMethod.Post, text)).code);

        var c = Client("free");

        // 2. /me before any refinement: free, 3 left, no chip.
        var me = await c.GetFromJsonAsync<JsonElement>($"/matches/{_matchId}/me", J);
        Assert.Equal("free", me.GetProperty("tier").GetString());
        Assert.Equal(3, me.GetProperty("quotaRemaining").GetInt32());
        Assert.Equal(JsonValueKind.Null, me.GetProperty("chip").ValueKind);

        // 3. Successful text refinement → applied, quota 3→2.
        StubEngine("success");
        var r1 = await Send(c, HttpMethod.Post, text);
        Assert.Equal("success", r1.body.GetProperty("status").GetString());
        Assert.True(r1.body.GetProperty("applied").GetBoolean());
        Assert.Equal(2, r1.body.GetProperty("quotaRemaining").GetInt32());

        // 4. Gibberish → not applied, NO credit consumed (still 2).
        StubEngine("rejected_gibberish");
        var r2 = await Send(c, HttpMethod.Post, text);
        Assert.False(r2.body.GetProperty("applied").GetBoolean());
        Assert.Equal(2, r2.body.GetProperty("quotaRemaining").GetInt32());

        // 5. Dead URL → not applied, NO credit (engine never even called).
        StubFetcher("dead_url", null);
        var r3 = await Send(c, HttpMethod.Post, url);
        Assert.Equal("dead_url", r3.body.GetProperty("status").GetString());
        Assert.Equal(2, r3.body.GetProperty("quotaRemaining").GetInt32());
        StubFetcher("ok", "text");

        // 6. Two more successes exhaust the free quota (2→1→0).
        StubEngine("success");
        Assert.Equal(1, (await Send(c, HttpMethod.Post, text)).body.GetProperty("quotaRemaining").GetInt32());
        Assert.Equal(0, (await Send(c, HttpMethod.Post, text)).body.GetProperty("quotaRemaining").GetInt32());

        // 7. 4th successful attempt is blocked → 429.
        var blocked = await Send(c, HttpMethod.Post, text);
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.code);
        Assert.Equal("quota_exhausted", blocked.body.GetProperty("status").GetString());

        // 8. Editing the active chip is a free re-run (PUT), quota stays 0.
        var edited = await Send(c, HttpMethod.Put, text);
        Assert.Equal("success", edited.body.GetProperty("status").GetString());
        Assert.Equal(0, edited.body.GetProperty("quotaRemaining").GetInt32());

        // 9. Remove → chip reverts to null.
        Assert.Equal(HttpStatusCode.OK, (await Send(c, HttpMethod.Delete, null)).code);
        var meAfter = await c.GetFromJsonAsync<JsonElement>($"/matches/{_matchId}/me", J);
        Assert.Equal(JsonValueKind.Null, meAfter.GetProperty("chip").ValueKind);

        // 10. Ledger charged exactly 3 (the successful POSTs only).
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WcDbContext>();
        var charged = await db.QuotaLedger.SumAsync(q => q.SuccessCount);
        Assert.Equal(3, charged);
    }
}
