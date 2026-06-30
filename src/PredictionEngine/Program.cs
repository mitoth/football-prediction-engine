using Quartz;
using WcPredictions.Data;
using WcPredictions.PredictionEngine;
using WcPredictions.PredictionEngine.Baseline;
using WcPredictions.PredictionEngine.Gateway;

// WebApplication (not a bare worker) so the engine can expose POST /refine for
// the BFF to orchestrate, while the scheduled Quartz baseline job still runs.
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<WcDbContext>("wcdb");
builder.AddRedisDistributedCache("cache");

// Typed client to the LLM Gateway. "https+http://llm-gateway" lets Aspire
// service discovery resolve the actual address (https preferred, http fallback).
// /predict + /refine wrap a Claude call (~20-40s over a 20-article context,
// MaxTokens 16000). The LLM-grade resilience timeout lives in ServiceDefaults
// (global); the long HttpClient.Timeout keeps the outer ceiling clear of it.
builder.Services.AddHttpClient<LlmGatewayClient>(c =>
{
    c.BaseAddress = new Uri("https+http://llm-gateway");
    c.Timeout = TimeSpan.FromMinutes(3);
});

builder.Services.AddScoped<BaselineService>();
builder.Services.AddScoped<RefinementService>();

builder.Services.AddQuartz(q =>
{
    var job = new JobKey("baseline-build");
    q.AddJob<BaselineJob>(j => j.WithIdentity(job));
    q.AddTrigger(t => t.ForJob(job).StartNow()
        .WithSimpleSchedule(s => s.WithIntervalInHours(1).RepeatForever()));
});
builder.Services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapGet("/", () => "WcPredictions Prediction Engine");

// Compute-only: BFF has already verified auth + quota and URL-extracted the
// note. Persistence of the Refinement row + snapshot is the BFF's job. The
// optional `Messages` array carries multi-turn chat history when the BFF used
// the /chat endpoint; otherwise UserNote is the single-shot legacy path.
app.MapPost("/refine", async (
    RefineHttpRequest req, RefinementService svc, CancellationToken ct) =>
{
    var result = await svc.RefineAsync(
        req.MatchId, req.BaselineId, req.UserNote, req.Messages, ct);
    return Results.Ok(result);
});

app.Run();

public sealed record RefineHttpRequest(
    Guid MatchId, Guid BaselineId, string UserNote,
    IReadOnlyList<ChatTurn>? Messages = null);

// Exposed so the Phase 4 integration test can drive the engine in-process.
public partial class Program;
