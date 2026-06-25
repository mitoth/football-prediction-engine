using System.Net.Http.Json;

namespace WcPredictions.Bff;

// HTTP contract mirror (BFF takes no dependency on the engine assembly —
// same pattern as the engine's LlmGatewayClient).

public sealed record RefineResult(
    string Status,                 // success | rejected_gibberish | off_topic
    double Home, double Draw, double Away,
    int PredHome, int PredAway,
    string Why, IReadOnlyList<string> Citations);

// Mirror of LlmGatewayClient.ChatTurn — kept here so the BFF can pass the chat
// history through to the engine without depending on the gateway assembly.
public sealed record EngineChatTurn(string Role, string Text);

public sealed class PredictionEngineClient(HttpClient http)
{
    public Task<RefineResult> RefineAsync(
        Guid matchId, Guid baselineId, string userNote, CancellationToken ct) =>
        RefineAsync(matchId, baselineId, userNote, messages: null, ct);

    public async Task<RefineResult> RefineAsync(
        Guid matchId, Guid baselineId, string userNote,
        IReadOnlyList<EngineChatTurn>? messages, CancellationToken ct)
    {
        var resp = await http.PostAsJsonAsync("/refine",
            new { matchId, baselineId, userNote, messages }, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<RefineResult>(ct)
               ?? throw new InvalidOperationException("Prediction Engine returned empty body");
    }
}
