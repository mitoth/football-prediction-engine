using System.Net.Http.Json;

namespace WcPredictions.PredictionEngine.Gateway;

// HTTP contract mirror of the LLM Gateway's /predict API (kept separate so the
// engine doesn't take a dependency on the Anthropic SDK).
public sealed record ArticleRef(string Id, string Headline, string Snippet);

public sealed record PredictRequest(
    string HomeTeam,
    string AwayTeam,
    string League,
    DateTimeOffset KickoffUtc,
    string? HomeForm,
    string? AwayForm,
    string? Lineups,
    IReadOnlyList<ArticleRef> Articles,
    string? UserInput,
    string? BaselineSummary);

public sealed record OutcomeProbs(double Home, double Draw, double Away);

public sealed record PredictResponse(
    OutcomeProbs OutcomeProbs,
    int PredHome,
    int PredAway,
    string Why,
    IReadOnlyList<string> Citations);

// Refinement adds the accepted/relevant classification on top of a prediction.
public sealed record RefineResponse(
    bool Accepted,
    bool Relevant,
    string RejectReason,
    OutcomeProbs OutcomeProbs,
    int PredHome,
    int PredAway,
    string Why,
    IReadOnlyList<string> Citations);

public sealed class LlmGatewayClient(HttpClient http)
{
    public async Task<PredictResponse> PredictAsync(PredictRequest req, CancellationToken ct)
    {
        var resp = await http.PostAsJsonAsync("/predict", req, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<PredictResponse>(ct)
               ?? throw new InvalidOperationException("Gateway returned empty body");
    }

    public async Task<RefineResponse> RefineAsync(PredictRequest req, CancellationToken ct)
    {
        var resp = await http.PostAsJsonAsync("/refine", req, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<RefineResponse>(ct)
               ?? throw new InvalidOperationException("Gateway returned empty body");
    }
}
