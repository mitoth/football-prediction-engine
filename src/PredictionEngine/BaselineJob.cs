using Microsoft.EntityFrameworkCore;
using Quartz;
using WcPredictions.Data;
using WcPredictions.PredictionEngine.Baseline;

namespace WcPredictions.PredictionEngine;

// Quartz wakes this job every hour (see Program.cs). Each match gets exactly
// three baselines, built as kickoff approaches: ~24h before, ~10h before, and
// ~1h before. Earlier builds set an initial prediction; the later two refresh
// it against newer news (lineups, injuries) closer to the game. The count of
// existing baselines tells us which builds are still owed — at each hourly
// run we build at most one per match, so a match catches up over consecutive
// runs if it was added late inside a window.
[DisallowConcurrentExecution]
public sealed class BaselineJob(IServiceScopeFactory scopes, ILogger<BaselineJob> log) : IJob
{
    // Lead times before kickoff at which a baseline should exist, largest first.
    // The number of these that have elapsed for a match == how many baselines
    // it should have by now.
    private static readonly TimeSpan[] BuildLeadTimes =
    [
        TimeSpan.FromHours(24),
        TimeSpan.FromHours(10),
        TimeSpan.FromHours(1),
    ];

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WcDbContext>();
        var baselines = scope.ServiceProvider.GetRequiredService<BaselineService>();

        var now = DateTimeOffset.UtcNow;
        // Only matches inside the first (largest) lead window are in play.
        var horizon = now.Add(BuildLeadTimes[0]);

        var candidates = await db.Matches
            .Where(m => m.KickoffUtc > now && m.KickoffUtc <= horizon)
            .Select(m => new { m.Id, m.KickoffUtc, Built = db.Baselines.Count(b => b.MatchId == m.Id) })
            .ToListAsync(ct);

        var built = 0;
        foreach (var c in candidates)
        {
            var timeToKickoff = c.KickoffUtc - now;
            // How many of the three builds are due by now (1..3).
            var due = BuildLeadTimes.Count(t => timeToKickoff <= t);
            if (c.Built >= due) continue; // up to date for this window

            try
            {
                await baselines.BuildAsync(c.Id, $"scheduled-build-{c.Built + 1}of3", ct);
                built++;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Baseline build failed for {MatchId}", c.Id);
            }
        }

        log.LogInformation(
            "BaselineJob: built {Built} baselines across {Count} upcoming matches", built, candidates.Count);
    }
}
