using Microsoft.Extensions.Options;
using Quartz;
using WcPredictions.Data;
using WcPredictions.Ingestion;
using WcPredictions.Ingestion.ApiFootball;
using WcPredictions.Ingestion.News;
using WcPredictions.Ingestion.Sync;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

// Aspire Npgsql EF client — "wcdb" matches the AppHost database resource.
builder.AddNpgsqlDbContext<WcDbContext>("wcdb");

builder.Services.Configure<ApiFootballOptions>(
    builder.Configuration.GetSection(ApiFootballOptions.Section));
builder.Services.Configure<NewsDataOptions>(
    builder.Configuration.GetSection(NewsDataOptions.Section));

// Typed HttpClients. ServiceDefaults adds standard resilience to all clients.
builder.Services.AddHttpClient<ApiFootballClient>((sp, c) =>
{
    var o = sp.GetRequiredService<IOptions<ApiFootballOptions>>().Value;
    c.BaseAddress = new Uri(o.BaseUrl);
    if (!string.IsNullOrEmpty(o.ApiKey))
        c.DefaultRequestHeaders.Add("x-apisports-key", o.ApiKey);
});
builder.Services.AddHttpClient<NewsDataClient>((sp, c) =>
{
    var o = sp.GetRequiredService<IOptions<NewsDataOptions>>().Value;
    c.BaseAddress = new Uri(o.BaseUrl);
});

builder.Services.AddScoped<FixtureSyncService>();
builder.Services.AddScoped<NewsSyncService>();

// Quartz: both jobs StartNow (initial sync = the World Cup seed) then repeat.
builder.Services.AddQuartz(q =>
{
    var fixture = new JobKey("fixture-sync");
    q.AddJob<FixtureSyncJob>(j => j.WithIdentity(fixture));
    q.AddTrigger(t => t.ForJob(fixture).StartNow()
        .WithSimpleSchedule(s => s.WithIntervalInHours(6).RepeatForever()));

    var news = new JobKey("news-sync");
    q.AddJob<NewsSyncJob>(j => j.WithIdentity(news));
    q.AddTrigger(t => t.ForJob(news).StartNow()
        .WithSimpleSchedule(s => s.WithIntervalInMinutes(30).RepeatForever()));
});
builder.Services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);

var host = builder.Build();
host.Run();
