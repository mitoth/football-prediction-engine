using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WcPredictions.Data;

namespace WcPredictions.Bff;

// Phase 3 read path. Anonymous, DB-backed: the baseline + citations are already
// persisted by the Prediction Engine (Phase 2), so the BFF serves them straight
// from Postgres — no cross-service HTTP, no Anthropic dependency in the BFF.

public sealed record MatchListItem(
    Guid Id, string League, string? Stage, string HomeTeam, string AwayTeam,
    DateTimeOffset KickoffUtc, string Status, bool HasBaseline,
    DateTimeOffset? BaselineGeneratedAt);

// Surfaces "when's the next refresh?" information in the UI. Both schedules
// are driven by Quartz simple-interval triggers configured in the Ingestion
// (news, 30 min) and PredictionEngine (baselines, 60 min) AppHost wire-up —
// values mirrored here as constants. NextAt = LastAt + interval; if LastAt
// is null (cold start), Next is null too and the UI shows "queued".
public sealed record SyncStatus(
    DateTimeOffset? NewsLastFetchedAt, DateTimeOffset? NewsNextFetchAt,
    int NewsIntervalMinutes,
    DateTimeOffset? BaselineLastBuiltAt, DateTimeOffset? BaselineNextBuildAt,
    int BaselineIntervalMinutes);

public sealed record CitationView(
    Guid ArticleId, string Headline, string Outlet, string Url, string Snippet);

public sealed record BaselineView(
    int Version, double Home, double Draw, double Away,
    int PredHome, int PredAway, string Why, IReadOnlyList<CitationView> Citations);

public sealed record MatchDetail(
    Guid Id, string League, string? Stage, string HomeTeam, string AwayTeam,
    DateTimeOffset KickoffUtc, string Status, BaselineView? Baseline);

public static class MatchEndpoints
{
    public static void MapMatchEndpoints(this WebApplication app)
    {
        // Upcoming fixtures, soonest first. Anonymous — the 10-second baseline
        // read is the front door (design §intro), no sign-in required.
        app.MapGet("/matches", async (WcDbContext db, CancellationToken ct) =>
        {
            var now = DateTimeOffset.UtcNow;
            var items = await db.Matches
                .Where(m => m.KickoffUtc >= now)
                .OrderBy(m => m.KickoffUtc)
                .Select(m => new MatchListItem(
                    m.Id, m.League.Name, m.Stage, m.HomeTeam.Name, m.AwayTeam.Name,
                    m.KickoffUtc, m.Status, m.Baselines.Any(),
                    m.Baselines.OrderByDescending(b => b.CreatedAt)
                               .Select(b => (DateTimeOffset?)b.CreatedAt)
                               .FirstOrDefault()))
                .ToListAsync(ct);
            return Results.Ok(items);
        });

        // Surfaces refresh cadence to the UI. Cheap query (two MAX scans on
        // small tables; the user-facing match list refreshes infrequently).
        //
        // "next ... at" is clamped to >= now. Without the clamp, when the
        // baseline engine ran an hour ago but found no work (all upcoming
        // matches already have a baseline), `last + interval` is in the past
        // and the UI shows "20 h ago" — misleading, since BaselineJob will
        // fire again on the next hourly tick. Clamping to now means the UI
        // reads "any moment" via the <1-min branch of the humanizer, which
        // is the truthful answer.
        app.MapGet("/meta/sync-status", async (WcDbContext db, CancellationToken ct) =>
        {
            const int newsIntervalMin = 30;       // Ingestion NewsSyncJob trigger
            const int baselineIntervalMin = 60;   // PredictionEngine BaselineJob trigger
            var now = DateTimeOffset.UtcNow;

            DateTimeOffset? newsLast = await db.Articles
                .OrderByDescending(a => a.FetchedAt)
                .Select(a => (DateTimeOffset?)a.FetchedAt)
                .FirstOrDefaultAsync(ct);
            DateTimeOffset? baselineLast = await db.Baselines
                .OrderByDescending(b => b.CreatedAt)
                .Select(b => (DateTimeOffset?)b.CreatedAt)
                .FirstOrDefaultAsync(ct);

            DateTimeOffset? ClampForward(DateTimeOffset? last, int intervalMin) =>
                last is null ? null
                    : last.Value.AddMinutes(intervalMin) > now
                        ? last.Value.AddMinutes(intervalMin)
                        : now;

            return Results.Ok(new SyncStatus(
                newsLast,
                ClampForward(newsLast, newsIntervalMin),
                newsIntervalMin,
                baselineLast,
                ClampForward(baselineLast, baselineIntervalMin),
                baselineIntervalMin));
        });

        // Match detail + the latest baseline version with resolved citations.
        app.MapGet("/matches/{id:guid}", async (Guid id, WcDbContext db, CancellationToken ct) =>
        {
            var m = await db.Matches
                .Where(x => x.Id == id)
                .Select(x => new
                {
                    x.Id,
                    League = x.League.Name,
                    x.Stage,
                    Home = x.HomeTeam.Name,
                    Away = x.AwayTeam.Name,
                    x.KickoffUtc,
                    x.Status,
                })
                .SingleOrDefaultAsync(ct);
            if (m is null) return Results.NotFound();

            var baseline = await db.Baselines
                .Where(b => b.MatchId == id)
                .OrderByDescending(b => b.Version)
                .Select(b => new
                {
                    b.Version,
                    b.OutcomeProbs,
                    b.PredHome,
                    b.PredAway,
                    b.WhyText,
                    Citations = b.Citations.Select(c => new CitationView(
                        c.Article.Id, c.Article.Headline, c.Article.Outlet,
                        c.Article.Url, c.Article.Snippet)).ToList(),
                })
                .FirstOrDefaultAsync(ct);

            BaselineView? view = null;
            if (baseline is not null)
            {
                using var probs = JsonDocument.Parse(baseline.OutcomeProbs);
                var r = probs.RootElement;
                view = new BaselineView(
                    baseline.Version,
                    r.GetProperty("home").GetDouble(),
                    r.GetProperty("draw").GetDouble(),
                    r.GetProperty("away").GetDouble(),
                    baseline.PredHome, baseline.PredAway, baseline.WhyText,
                    baseline.Citations);
            }

            return Results.Ok(new MatchDetail(
                m.Id, m.League, m.Stage, m.Home, m.Away, m.KickoffUtc, m.Status, view));
        });
    }
}
