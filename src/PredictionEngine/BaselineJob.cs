using Microsoft.EntityFrameworkCore;
using Quartz;
using WcPredictions.Data;
using WcPredictions.PredictionEngine.Baseline;

namespace WcPredictions.PredictionEngine;

// Builds a baseline for every upcoming match that doesn't have one yet.
// Per-match T-24h / T-1h refresh triggers are a later refinement; Phase 2
// establishes generation + versioning + caching.
[DisallowConcurrentExecution]
public sealed class BaselineJob(IServiceScopeFactory scopes, ILogger<BaselineJob> log) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WcDbContext>();
        var baselines = scope.ServiceProvider.GetRequiredService<BaselineService>();

        var now = DateTimeOffset.UtcNow;
        var horizon = now.AddDays(14);
        var matchIds = await db.Matches
            .Where(m => m.KickoffUtc > now && m.KickoffUtc < horizon
                        && !db.Baselines.Any(b => b.MatchId == m.Id))
            .Select(m => m.Id)
            .ToListAsync(ct);

        log.LogInformation("BaselineJob: {Count} matches need a baseline", matchIds.Count);
        foreach (var id in matchIds)
        {
            try
            {
                await baselines.BuildAsync(id, "scheduled", ct);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Baseline build failed for {MatchId}", id);
            }
        }
    }
}
