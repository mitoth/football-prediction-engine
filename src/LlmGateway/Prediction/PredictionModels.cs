namespace WcPredictions.LlmGateway.Prediction;

// Gateway API contract. The Prediction Engine POSTs a PredictRequest and gets
// back a PredictResponse. Trusted context vs. untrusted evidence are separated
// so the system prompt can forbid instruction-following inside the latter.

public sealed record ArticleRef(string Id, string Headline, string Snippet);

// One turn of a refinement chat. Role is "user" or "assistant"; Text is the
// user's natural-language note or the assistant's short reasoning summary.
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
    // Set only for a refinement: the user's free text / extracted URL content
    // plus a short summary of the baseline being refined.
    string? UserInput,
    string? BaselineSummary,
    // Multi-turn chat history (last entry must be a user message). When null,
    // the gateway falls back to the legacy single-shot UserInput path.
    IReadOnlyList<ChatTurn>? Messages = null);

public sealed record OutcomeProbs(double Home, double Draw, double Away);

public sealed record PredictResponse(
    OutcomeProbs OutcomeProbs,
    int PredHome,
    int PredAway,
    string Why,
    IReadOnlyList<string> Citations);

// Refinement adds a classification of the user's note on top of a prediction.
// Accepted=false → gibberish; Relevant=false → off-topic. The caller spends a
// quota credit only when both are true (RejectReason is "" in that case).
public sealed record RefineResponse(
    bool Accepted,
    bool Relevant,
    string RejectReason,
    OutcomeProbs OutcomeProbs,
    int PredHome,
    int PredAway,
    string Why,
    IReadOnlyList<string> Citations);
