using Quartz;
using WcPredictions.Data;
using WcPredictions.PredictionEngine;
using WcPredictions.PredictionEngine.Baseline;
using WcPredictions.PredictionEngine.Gateway;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<WcDbContext>("wcdb");
builder.AddRedisDistributedCache("cache");

// Typed client to the LLM Gateway. "https+http://llm-gateway" lets Aspire
// service discovery resolve the actual address (https preferred, http fallback).
builder.Services.AddHttpClient<LlmGatewayClient>(c =>
    c.BaseAddress = new Uri("https+http://llm-gateway"));

builder.Services.AddScoped<BaselineService>();

builder.Services.AddQuartz(q =>
{
    var job = new JobKey("baseline-build");
    q.AddJob<BaselineJob>(j => j.WithIdentity(job));
    q.AddTrigger(t => t.ForJob(job).StartNow()
        .WithSimpleSchedule(s => s.WithIntervalInHours(1).RepeatForever()));
});
builder.Services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);

var host = builder.Build();
host.Run();
