using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using WcPredictions.Data;
using WcPredictions.PredictionEngine.Gateway;

namespace WcPredictions.PredictionEngine.Baseline;

// What callers and the Redis cache see — the current baseline for a match.
public sealed record BaselineDto(
    Guid Id, int Version, double Home, double Draw, double Away,
    int PredHome, int PredAway, string Why, IReadOnlyList<string> Citations);

public sealed class BaselineService(
    WcDbContext db,
    LlmGatewayClient gateway,
    IDistributedCache cache,
    ILogger<BaselineService> log)
{
    private const int ArticleContextSize = 8;
    private static readonly DistributedCacheEntryOptions CacheTtl = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6),
    };

    private static string CacheKey(Guid matchId) => $"baseline:{matchId}";

    // The single source of truth for "this article mentions this match". Used
    // both to feed the LLM in BuildAsync and to detect news growth in the
    // refresh path (BaselineJob). Keep these in sync — otherwise the counts
    // diverge and we either refresh too eagerly or starve.
    public static IQueryable<Article> RelevantArticles(WcDbContext db, string home, string away) =>
        db.Articles.Where(a =>
            EF.Functions.ILike(a.Headline, $"%{home}%") ||
            EF.Functions.ILike(a.Headline, $"%{away}%") ||
            EF.Functions.ILike(a.Snippet,  $"%{home}%") ||
            EF.Functions.ILike(a.Snippet,  $"%{away}%"));

    // Read-through: a cached baseline is returned WITHOUT calling the gateway
    // (the §16 "second request = Redis hit, no second LLM call" guarantee).
    public async Task<BaselineDto> GetOrBuildAsync(Guid matchId, CancellationToken ct)
    {
        var hit = await cache.GetStringAsync(CacheKey(matchId), ct);
        if (hit is not null)
        {
            log.LogInformation("Baseline cache hit for {MatchId}", matchId);
            return JsonSerializer.Deserialize<BaselineDto>(hit)!;
        }
        return await BuildAsync(matchId, "cache-miss", ct);
    }

    public async Task<BaselineDto> BuildAsync(Guid matchId, string trigger, CancellationToken ct)
    {
        var match = await db.Matches
            .Include(m => m.HomeTeam).Include(m => m.AwayTeam).Include(m => m.League)
            .SingleOrDefaultAsync(m => m.Id == matchId, ct)
            ?? throw new InvalidOperationException($"Match {matchId} not found");

        // Pick articles that mention either team in the headline or snippet.
        // Generic "football OR soccer" feed dominates the global Article table —
        // without this filter, Claude gets unrelated news and cites nothing.
        // Falls back to the newest articles if no team-specific hit exists.
        var home = match.HomeTeam.Name;
        var away = match.AwayTeam.Name;
        var relevantQ = RelevantArticles(db, home, away);
        // Total count drives the refresh decision (see BaselineJob); the
        // capped slice goes to the LLM.
        var relevantCount = await relevantQ.CountAsync(ct);
        var relevant = await relevantQ
            .OrderByDescending(a => a.FetchedAt)
            .Take(ArticleContextSize)
            .ToListAsync(ct);
        var articles = relevant.Count > 0
            ? relevant
            : await db.Articles
                .OrderByDescending(a => a.FetchedAt)
                .Take(ArticleContextSize)
                .ToListAsync(ct);

        var req = new PredictRequest(
            match.HomeTeam.Name, match.AwayTeam.Name, match.League.Name,
            match.KickoffUtc, HomeForm: null, AwayForm: null, Lineups: null,
            articles.Select(a => new ArticleRef(a.Id.ToString(), a.Headline, a.Snippet)).ToList(),
            UserInput: null, BaselineSummary: null);

        var resp = await gateway.PredictAsync(req, ct);

        // Validate + normalize probabilities so they always sum to 1.0.
        var sum = resp.OutcomeProbs.Home + resp.OutcomeProbs.Draw + resp.OutcomeProbs.Away;
        if (sum <= 0) throw new InvalidOperationException("Gateway returned non-positive probabilities");
        double h = resp.OutcomeProbs.Home / sum, d = resp.OutcomeProbs.Draw / sum, a2 = resp.OutcomeProbs.Away / sum;

        // Citations must resolve to real Article rows we supplied.
        var sentIds = articles.ToDictionary(x => x.Id.ToString(), x => x.Id);
        var citedArticleIds = resp.Citations
            .Where(sentIds.ContainsKey).Select(c => sentIds[c]).Distinct().ToList();

        // The "what the model read" surface (design §3 — "this site read 8
        // articles for me") shows every article fed to the prompt, not only
        // the ones Claude explicitly returned. Persist all inputs so the user
        // can audit the context even when the model doesn't cite anything.
        var readArticleIds = articles.Select(a => a.Id).ToList();

        var nextVersion = ((await db.Baselines
            .Where(b => b.MatchId == matchId)
            .Select(b => (int?)b.Version).MaxAsync(ct)) ?? 0) + 1;

        var probsJson = JsonSerializer.Serialize(new { home = h, draw = d, away = a2 });
        var baseline = new Data.Baseline
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            Version = nextVersion,
            OutcomeProbs = probsJson,
            PredHome = resp.PredHome,
            PredAway = resp.PredAway,
            Confidence = Math.Max(h, Math.Max(d, a2)),
            WhyText = resp.Why,
            RefreshTrigger = trigger,
            CreatedAt = DateTimeOffset.UtcNow,
            RelevantArticleCount = relevantCount,
        };
        db.Baselines.Add(baseline);
        foreach (var articleId in readArticleIds)
            db.BaselineCitations.Add(new BaselineCitation { BaselineId = baseline.Id, ArticleId = articleId });

        db.PredictionSnapshots.Add(new PredictionSnapshot
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            SourceKind = "baseline",
            BaselineId = baseline.Id,
            RefinementId = null,
            OutcomeProbs = probsJson,
            PredHome = resp.PredHome,
            PredAway = resp.PredAway,
            CapturedAt = DateTimeOffset.UtcNow,
        });

        await db.SaveChangesAsync(ct);

        var dto = new BaselineDto(
            baseline.Id, nextVersion, h, d, a2,
            resp.PredHome, resp.PredAway, resp.Why,
            readArticleIds.Select(x => x.ToString()).ToList());
        await cache.SetStringAsync(CacheKey(matchId), JsonSerializer.Serialize(dto), CacheTtl, ct);

        log.LogInformation(
            "Baseline v{Version} built for {MatchId} (trigger={Trigger}, {Read} articles read, {Cites} cited)",
            nextVersion, matchId, trigger, readArticleIds.Count, citedArticleIds.Count);
        return dto;
    }
}
