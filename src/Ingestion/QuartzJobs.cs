using Quartz;
using WcPredictions.Ingestion.Sync;

namespace WcPredictions.Ingestion;

[DisallowConcurrentExecution]
public sealed class FixtureSyncJob(IServiceScopeFactory scopes) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        using var scope = scopes.CreateScope();
        await scope.ServiceProvider.GetRequiredService<FixtureSyncService>()
            .SyncAsync(context.CancellationToken);
    }
}

[DisallowConcurrentExecution]
public sealed class NewsSyncJob(IServiceScopeFactory scopes) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        using var scope = scopes.CreateScope();
        await scope.ServiceProvider.GetRequiredService<NewsSyncService>()
            .SyncAsync(context.CancellationToken);
    }
}
