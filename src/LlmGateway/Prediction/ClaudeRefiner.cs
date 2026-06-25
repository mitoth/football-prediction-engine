using System.Text.Json;
using System.Text.Json.Serialization;
using Anthropic;
using Anthropic.Models.Messages;

namespace WcPredictions.LlmGateway.Prediction;

// Refinement twin of ClaudePredictor: same untrusted-input quarantine, prompt
// cache, and citation enforcement, but the refinement schema/prompt (which add
// the accepted/relevant classification). Cost counters are shared via the same
// Meter so refinement spend shows up in the same dashboard.
public sealed class ClaudeRefiner(
    AnthropicClient client,
    IConfiguration config,
    ILogger<ClaudeRefiner> log)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public async Task<RefineResponse> RefineAsync(PredictRequest req, CancellationToken ct)
    {
        var model = config["Anthropic:Model"] ?? "claude-opus-4-7";

        var parameters = new MessageCreateParams
        {
            Model = model,
            MaxTokens = 16000,
            System = new List<TextBlockParam>
            {
                new() { Text = RefinementPrompt.System, CacheControl = new CacheControlEphemeral() },
            },
            Messages = BuildMessages(req),
            Thinking = new ThinkingConfigAdaptive(),
            OutputConfig = new OutputConfig
            {
                Effort = Effort.High,
                Format = new JsonOutputFormat
                {
                    Schema = new Dictionary<string, JsonElement>(RefinementPrompt.Schema),
                },
            },
        };

        Message response = await client.Messages.Create(parameters, cancellationToken: ct);

        var usage = response.Usage;
        ClaudePredictor.RecordUsage(usage);
        log.LogInformation(
            "Claude refinement: in={In} out={Out} cacheRead={CR} cacheWrite={CW} model={Model}",
            usage.InputTokens, usage.OutputTokens,
            usage.CacheReadInputTokens ?? 0, usage.CacheCreationInputTokens ?? 0, model);

        var json = response.Content.Select(b => b.Value).OfType<TextBlock>().FirstOrDefault()?.Text
                   ?? throw new InvalidOperationException("Claude returned no text block");

        var dto = JsonSerializer.Deserialize<Dto>(json, Json)
                  ?? throw new InvalidOperationException("Claude returned unparseable JSON");

        var allowed = req.Articles.Select(a => a.Id).ToHashSet();
        var citations = (dto.Citations ?? []).Where(allowed.Contains).Distinct().ToList();

        return new RefineResponse(
            dto.Accepted, dto.Relevant, dto.RejectReason ?? "",
            new OutcomeProbs(dto.OutcomeProbs.Home, dto.OutcomeProbs.Draw, dto.OutcomeProbs.Away),
            dto.PredHome, dto.PredAway, dto.Why, citations);
    }

    // Single-shot mode (legacy /predict + initial /refine) packs TrustedContext +
    // UntrustedBlock into one user turn. Chat mode unpacks the thread into
    // alternating user/assistant turns; the first user turn keeps the trusted
    // context + articles so the model anchors on the same data each round.
    private static List<MessageParam> BuildMessages(PredictRequest req)
    {
        if (req.Messages is null || req.Messages.Count == 0)
        {
            return new List<MessageParam>
            {
                new()
                {
                    Role = Role.User,
                    Content = new List<ContentBlockParam>
                    {
                        new TextBlockParam { Text = PredictionPrompt.TrustedContext(req) },
                        new TextBlockParam { Text = PredictionPrompt.UntrustedBlock(req) },
                    },
                },
            };
        }

        var messages = new List<MessageParam>();
        var trustedPrefix = PredictionPrompt.TrustedContext(req)
                          + PredictionPrompt.UntrustedBlock(req, includeUserNote: false);

        for (var i = 0; i < req.Messages.Count; i++)
        {
            var turn = req.Messages[i];
            var isFirst = i == 0;
            var role = string.Equals(turn.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                ? Role.Assistant
                : Role.User;

            // The very first user turn carries TrustedContext + articles so the
            // cached prefix anchors all subsequent turns. Each later user turn
            // is just the natural-language note in a `<user_note>` envelope so
            // the system prompt's injection guard still binds.
            var text = role == Role.User
                ? (isFirst
                    ? trustedPrefix + $"<user_note>{turn.Text}</user_note>"
                    : $"<user_note>{turn.Text}</user_note>")
                : turn.Text;

            messages.Add(new MessageParam
            {
                Role = role,
                Content = new List<ContentBlockParam>
                {
                    new TextBlockParam { Text = text },
                },
            });
        }

        return messages;
    }

    private sealed record Dto(
        [property: JsonPropertyName("accepted")] bool Accepted,
        [property: JsonPropertyName("relevant")] bool Relevant,
        [property: JsonPropertyName("reject_reason")] string? RejectReason,
        [property: JsonPropertyName("outcome_probs")] ProbsDto OutcomeProbs,
        [property: JsonPropertyName("pred_home")] int PredHome,
        [property: JsonPropertyName("pred_away")] int PredAway,
        [property: JsonPropertyName("why")] string Why,
        [property: JsonPropertyName("citations")] List<string>? Citations);

    private sealed record ProbsDto(double Home, double Draw, double Away);
}
