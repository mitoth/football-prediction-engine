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
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = new List<ContentBlockParam>
                    {
                        new TextBlockParam { Text = PredictionPrompt.TrustedContext(req) },
                        new TextBlockParam { Text = PredictionPrompt.UntrustedBlock(req) },
                    },
                },
            ],
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
