using System.Net.Http.Json;

namespace WcPredictions.Bff;

// HTTP contract mirrors (BFF takes no dependency on the engine/fetcher
// assemblies — same pattern as the engine's LlmGatewayClient).

public sealed record FetchResult(string Status, string? Text); // ok|dead_url|blocked

public sealed class UrlFetcherClient(HttpClient http)
{
    public async Task<FetchResult> FetchAsync(string url, CancellationToken ct)
    {
        var resp = await http.PostAsJsonAsync("/fetch", new { url }, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<FetchResult>(ct)
               ?? throw new InvalidOperationException("URL Fetcher returned empty body");
    }
}

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
