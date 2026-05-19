namespace WcPredictions.LlmGateway.Prediction;

// Gateway API contract. The Prediction Engine POSTs a PredictRequest and gets
// back a PredictResponse. Trusted context vs. untrusted evidence are separated
// so the system prompt can forbid instruction-following inside the latter.

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
    // Set only for a refinement: the user's free text / extracted URL content
    // plus a short summary of the baseline being refined.
    string? UserInput,
    string? BaselineSummary);

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
