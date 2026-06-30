using Microsoft.Extensions.Http.Resilience;
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
builder.Services.AddHttpClient<LlmGatewayClient>(c =>
{
    c.BaseAddress = new Uri("https+http://llm-gateway");
    c.Timeout = TimeSpan.FromMinutes(3);
});
// ServiceDefaults applies AddStandardResilienceHandler() to every client — a
// 30s total timeout. An LLM prediction over a 20-article context (MaxTokens
// 16000) routinely needs longer, and the heaviest matches (France, Belgium)
// were timing out and getting NO baseline. Give this client (predict + refine)
// LLM-grade headroom. Options are keyed by the typed-client name.
builder.Services.Configure<HttpStandardResilienceOptions>("LlmGatewayClient", o =>
{
    o.Retry.MaxRetryAttempts = 1;
    o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(100);
    o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);
    // Must be >= 2x AttemptTimeout; keep clear of the boundary.
    o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(240);
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
