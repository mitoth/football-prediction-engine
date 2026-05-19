using Anthropic;
using OpenTelemetry.Metrics;
using WcPredictions.LlmGateway.Prediction;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// AnthropicClient reads ANTHROPIC_API_KEY (and ANTHROPIC_BASE_URL for tests)
// from the environment. The key is injected by the AppHost as a secret param.
builder.Services.AddSingleton(_ => new AnthropicClient());
builder.Services.AddScoped<ClaudePredictor>();
builder.Services.AddScoped<ClaudeRefiner>();

// Surface the LLM cost counters in the Aspire dashboard.
builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m.AddMeter(ClaudePredictor.MeterName));

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapGet("/", () => "WcPredictions LLM Gateway");

app.MapPost("/predict", async (
    PredictRequest req, ClaudePredictor predictor, CancellationToken ct) =>
{
    var result = await predictor.PredictAsync(req, ct);
    return Results.Ok(result);
});

// Refinement: same request shape (UserInput + BaselineSummary populated),
// returns the prediction plus the accepted/relevant classification.
app.MapPost("/refine", async (
    PredictRequest req, ClaudeRefiner refiner, CancellationToken ct) =>
{
    var result = await refiner.RefineAsync(req, ct);
    return Results.Ok(result);
});

app.Run();
