using System.Net.Http.Json;

namespace WcPredictions.PredictionEngine.Gateway;

// HTTP contract mirror of the LLM Gateway's /predict API (kept separate so the
// engine doesn't take a dependency on the Anthropic SDK).
public sealed record ArticleRef(string Id, string Headline, string Snippet);

// One turn of the refinement chat. Role is "user" or "assistant"; Text is the
// user's natural-language note for user turns, or a short natural-language
// summary (typically the prior turn's `why` paragraph) for assistant turns.
public sealed record ChatTurn(string Role, string Text);

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
    string? BaselineSummary,
    // Multi-turn chat history (last entry must be the new user message). When
    // null, the gateway falls back to the legacy single-shot UserInput path.
    IReadOnlyList<ChatTurn>? Messages = null);

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
