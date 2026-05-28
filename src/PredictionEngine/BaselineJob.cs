using Microsoft.EntityFrameworkCore;
using Quartz;
using WcPredictions.Data;
using WcPredictions.PredictionEngine.Baseline;

namespace WcPredictions.PredictionEngine;

// Quartz wakes this job every hour (see Program.cs). Two responsibilities:
//
//   1. **Build** — for matches with kickoff in the next 2 days that don't
//      have a baseline yet. Tighter than the original 14-day window to
//      avoid spending Anthropic budget on predictions that will be stale
//      by the time anyone reads them.
//
//   2. **Refresh** — for matches with kickoff in the next 1 day that do
//      have a baseline. Compare the current count of articles mentioning
//      either team against the count we stored at last build. If new
//      coverage has dropped, rebuild. The hourly trigger gives us the
//      "at most once per hour" cadence the user asked for; the count
//      check guarantees we don't burn tokens on quiet news days.
[DisallowConcurrentExecution]
public sealed class BaselineJob(IServiceScopeFactory scopes, ILogger<BaselineJob> log) : IJob
{
    private static readonly TimeSpan BuildHorizon   = TimeSpan.FromDays(2);
    private static readonly TimeSpan RefreshHorizon = TimeSpan.FromDays(1);

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WcDbContext>();
        var baselines = scope.ServiceProvider.GetRequiredService<BaselineService>();

        var now = DateTimeOffset.UtcNow;
        var buildHorizon = now.Add(BuildHorizon);
        var refreshHorizon = now.Add(RefreshHorizon);

        // --- 1. Build for T-2d matches without baseline -----------------------
        var toBuild = await db.Matches
            .Where(m => m.KickoffUtc > now && m.KickoffUtc < buildHorizon
                        && !db.Baselines.Any(b => b.MatchId == m.Id))
            .Select(m => m.Id)
            .ToListAsync(ct);

        log.LogInformation("BaselineJob: {Count} matches need a fresh baseline", toBuild.Count);
        foreach (var id in toBuild)
        {
            try { await baselines.BuildAsync(id, "scheduled-build", ct); }
            catch (Exception ex) { log.LogError(ex, "Baseline build failed for {MatchId}", id); }
        }

        // --- 2. Refresh stale baselines for T-1d matches ----------------------
        // Project the latest baseline per match alongside the team names so we
        // can re-run the relevant-article query in one pass per match.
        var refreshCandidates = await db.Matches
            .Where(m => m.KickoffUtc > now && m.KickoffUtc < refreshHorizon
                        && db.Baselines.Any(b => b.MatchId == m.Id))
            .Select(m => new
            {
                m.Id,
                Home = m.HomeTeam.Name,
                Away = m.AwayTeam.Name,
                Latest = db.Baselines
                    .Where(b => b.MatchId == m.Id)
                    .OrderByDescending(b => b.Version)
                    .Select(b => new { b.Id, b.RelevantArticleCount })
                    .First(),
            })
            .ToListAsync(ct);

        var refreshed = 0;
        foreach (var c in refreshCandidates)
        {
            int currentCount;
            try
            {
                currentCount = await BaselineService.RelevantArticles(db, c.Home, c.Away).CountAsync(ct);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Relevant-count failed for {MatchId}", c.Id);
                continue;
            }

            if (currentCount <= c.Latest.RelevantArticleCount) continue;

            try
            {
                await baselines.BuildAsync(c.Id, "scheduled-refresh", ct);
                refreshed++;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Baseline refresh failed for {MatchId}", c.Id);
            }
        }

        if (refreshed > 0)
            log.LogInformation("BaselineJob: refreshed {Count} stale baselines (news grew)", refreshed);
    }
}
