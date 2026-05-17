using Microsoft.EntityFrameworkCore;
using WcPredictions.Data;

namespace WcPredictions.Bff;

// Phase 0: applies EF migrations on startup. A dedicated migration resource with
// WaitForCompletion is a later-phase refinement; single-writer is fine for now.
public class DbMigrator(IServiceProvider services, ILogger<DbMigrator> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WcDbContext>();
        logger.LogInformation("Applying database migrations...");
        await db.Database.MigrateAsync(stoppingToken);
        logger.LogInformation("Database migrations applied.");
    }
}
