using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WcPredictions.Data;
using WcPredictions.PredictionEngine.Gateway;

namespace WcPredictions.PredictionEngine.Baseline;

// Pure compute: given a match, the baseline version being refined, and the
// user's note (already URL-extracted by the BFF), call the gateway and return
// the classification + refined prediction. NO persistence and NO quota here —
// those are user-scoped and owned by the BFF (which knows the Clerk identity).
public sealed record RefineResult(
    string Status,                 // success | rejected_gibberish | off_topic
    double Home, double Draw, double Away,
    int PredHome, int PredAway,
    string Why, IReadOnlyList<string> Citations);

public sealed class RefinementService(
    WcDbContext db,
    LlmGatewayClient gateway,
    ILogger<RefinementService> log)
{
    private const int ArticleContextSize = 8;

    public Task<RefineResult> RefineAsync(
        Guid matchId, Guid baselineId, string userNote, CancellationToken ct) =>
        RefineAsync(matchId, baselineId, userNote, messages: null, ct);

    public async Task<RefineResult> RefineAsync(
        Guid matchId, Guid baselineId, string userNote,
        IReadOnlyList<ChatTurn>? messages, CancellationToken ct)
    {
        var match = await db.Matches
            .Include(m => m.HomeTeam).Include(m => m.AwayTeam).Include(m => m.League)
            .SingleOrDefaultAsync(m => m.Id == matchId, ct)
            ?? throw new InvalidOperationException($"Match {matchId} not found");

        var baseline = await db.Baselines
            .SingleOrDefaultAsync(b => b.Id == baselineId && b.MatchId == matchId, ct)
            ?? throw new InvalidOperationException($"Baseline {baselineId} not found for match {matchId}");

        // Only news that names one of THIS match's teams — never the global
        // newest. The RSS feeds are general football; an unfiltered Take(8)
        // hands Claude articles about other fixtures and it refines on noise.
        // Shares BaselineService.RelevantArticles so baseline + refine see the
        // same relevance rule. Empty is fine: refine on the user note alone.
        var articles = await BaselineService
            .RelevantArticles(db, match.HomeTeam.Name, match.AwayTeam.Name)
            .OrderByDescending(a => a.FetchedAt)
            .Take(ArticleContextSize)
            .ToListAsync(ct);

        using var bp = JsonDocument.Parse(baseline.OutcomeProbs);
        var br = bp.RootElement;
        var baselineSummary =
            $"Outcome {br.GetProperty("home").GetDouble():P0} home / " +
            $"{br.GetProperty("draw").GetDouble():P0} draw / " +
            $"{br.GetProperty("away").GetDouble():P0} away, " +
            $"scoreline {baseline.PredHome}-{baseline.PredAway}. {baseline.WhyText}";

        var req = new PredictRequest(
            match.HomeTeam.Name, match.AwayTeam.Name, match.League.Name,
            match.KickoffUtc, HomeForm: null, AwayForm: null, Lineups: null,
            articles.Select(a => new ArticleRef(a.Id.ToString(), a.Headline, a.Snippet)).ToList(),
            UserInput: userNote, BaselineSummary: baselineSummary,
            Messages: messages);

        var resp = await gateway.RefineAsync(req, ct);

        var status = !resp.Accepted ? "rejected_gibberish"
                   : !resp.Relevant ? "off_topic"
                   : "success";

        if (status != "success")
        {
            log.LogInformation("Refinement for {MatchId} not applied ({Status})", matchId, status);
            // Echo the baseline so the caller can render it unchanged.
            return new RefineResult(status,
                br.GetProperty("home").GetDouble(), br.GetProperty("draw").GetDouble(),
                br.GetProperty("away").GetDouble(), baseline.PredHome, baseline.PredAway,
                baseline.WhyText, []);
        }

        var sum = resp.OutcomeProbs.Home + resp.OutcomeProbs.Draw + resp.OutcomeProbs.Away;
        if (sum <= 0) throw new InvalidOperationException("Gateway returned non-positive probabilities");
        double h = resp.OutcomeProbs.Home / sum, d = resp.OutcomeProbs.Draw / sum, a = resp.OutcomeProbs.Away / sum;

        var sentIds = articles.Select(x => x.Id.ToString()).ToHashSet();
        var citations = resp.Citations.Where(sentIds.Contains).Distinct().ToList();

        log.LogInformation("Refinement applied for {MatchId} ({Cites} citations)", matchId, citations.Count);
        return new RefineResult("success", h, d, a, resp.PredHome, resp.PredAway, resp.Why, citations);
    }
}
