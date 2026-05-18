using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Anthropic;
using Anthropic.Models.Messages;

namespace WcPredictions.LlmGateway.Prediction;

public sealed class ClaudePredictor(
    AnthropicClient client,
    IConfiguration config,
    ILogger<ClaudePredictor> log)
{
    public const string MeterName = "WcPredictions.LlmGateway";
    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> InputTokens = Meter.CreateCounter<long>("llm.tokens.input");
    private static readonly Counter<long> OutputTokens = Meter.CreateCounter<long>("llm.tokens.output");
    private static readonly Counter<long> CacheReadTokens = Meter.CreateCounter<long>("llm.tokens.cache_read");
    private static readonly Counter<long> CacheWriteTokens = Meter.CreateCounter<long>("llm.tokens.cache_write");

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public async Task<PredictResponse> PredictAsync(PredictRequest req, CancellationToken ct)
    {
        var model = config["Anthropic:Model"] ?? "claude-opus-4-7";

        var parameters = new MessageCreateParams
        {
            Model = model,
            MaxTokens = 16000,
            // Single stable, cached system block (prompt-cache prefix).
            System = new List<TextBlockParam>
            {
                new() { Text = PredictionPrompt.System, CacheControl = new CacheControlEphemeral() },
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
                    Schema = new Dictionary<string, JsonElement>(PredictionPrompt.Schema),
                },
            },
        };

        Message response = await client.Messages.Create(parameters, cancellationToken: ct);

        var usage = response.Usage;
        InputTokens.Add(usage.InputTokens);
        OutputTokens.Add(usage.OutputTokens);
        CacheReadTokens.Add(usage.CacheReadInputTokens ?? 0);
        CacheWriteTokens.Add(usage.CacheCreationInputTokens ?? 0);
        log.LogInformation(
            "Claude prediction: in={In} out={Out} cacheRead={CR} cacheWrite={CW} model={Model}",
            usage.InputTokens, usage.OutputTokens,
            usage.CacheReadInputTokens ?? 0, usage.CacheCreationInputTokens ?? 0, model);

        var json = response.Content.Select(b => b.Value).OfType<TextBlock>().FirstOrDefault()?.Text
                   ?? throw new InvalidOperationException("Claude returned no text block");

        var dto = JsonSerializer.Deserialize<Dto>(json, Json)
                  ?? throw new InvalidOperationException("Claude returned unparseable JSON");

        // Defence in depth: the model is told to only cite supplied ids; enforce
        // it here too so an invented id can never reach the caller.
        var allowed = req.Articles.Select(a => a.Id).ToHashSet();
        var citations = (dto.Citations ?? []).Where(allowed.Contains).Distinct().ToList();

        return new PredictResponse(
            new OutcomeProbs(dto.OutcomeProbs.Home, dto.OutcomeProbs.Draw, dto.OutcomeProbs.Away),
            dto.PredHome, dto.PredAway, dto.Why, citations);
    }

    private sealed record Dto(
        [property: JsonPropertyName("outcome_probs")] ProbsDto OutcomeProbs,
        [property: JsonPropertyName("pred_home")] int PredHome,
        [property: JsonPropertyName("pred_away")] int PredAway,
        [property: JsonPropertyName("why")] string Why,
        [property: JsonPropertyName("citations")] List<string>? Citations);

    private sealed record ProbsDto(double Home, double Draw, double Away);
}
