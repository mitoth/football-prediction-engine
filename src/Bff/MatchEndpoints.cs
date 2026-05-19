using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WcPredictions.Data;

namespace WcPredictions.Bff;

// Phase 3 read path. Anonymous, DB-backed: the baseline + citations are already
// persisted by the Prediction Engine (Phase 2), so the BFF serves them straight
// from Postgres — no cross-service HTTP, no Anthropic dependency in the BFF.

public sealed record MatchListItem(
    Guid Id, string League, string HomeTeam, string AwayTeam,
    DateTimeOffset KickoffUtc, string Status, bool HasBaseline);

public sealed record CitationView(
    Guid ArticleId, string Headline, string Outlet, string Url, string Snippet);

public sealed record BaselineView(
    int Version, double Home, double Draw, double Away,
    int PredHome, int PredAway, string Why, IReadOnlyList<CitationView> Citations);

public sealed record MatchDetail(
    Guid Id, string League, string HomeTeam, string AwayTeam,
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
                    m.Id, m.League.Name, m.HomeTeam.Name, m.AwayTeam.Name,
                    m.KickoffUtc, m.Status, m.Baselines.Any()))
                .ToListAsync(ct);
            return Results.Ok(items);
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
                m.Id, m.League, m.Home, m.Away, m.KickoffUtc, m.Status, view));
        });
    }
}
