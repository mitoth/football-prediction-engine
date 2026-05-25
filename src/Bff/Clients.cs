using System.Net.Http.Json;

namespace WcPredictions.Bff;

// HTTP contract mirror (BFF takes no dependency on the engine assembly —
// same pattern as the engine's LlmGatewayClient).

public sealed record RefineResult(
    string Status,                 // success | rejected_gibberish | off_topic
    double Home, double Draw, double Away,
    int PredHome, int PredAway,
    string Why, IReadOnlyList<string> Citations);

public sealed class PredictionEngineClient(HttpClient http)
{
    public async Task<RefineResult> RefineAsync(
        Guid matchId, Guid baselineId, string userNote, CancellationToken ct)
    {
        var resp = await http.PostAsJsonAsync("/refine",
            new { matchId, baselineId, userNote }, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<RefineResult>(ct)
               ?? throw new InvalidOperationException("Prediction Engine returned empty body");
    }
}
