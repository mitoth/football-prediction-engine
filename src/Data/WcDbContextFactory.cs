using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WcPredictions.Data;

// Design-time only: lets `dotnet ef migrations` build the model without a host
// or a live database. Runtime config comes from the Aspire Npgsql EF client.
public class WcDbContextFactory : IDesignTimeDbContextFactory<WcDbContext>
{
    public WcDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<WcDbContext>()
            .UseNpgsql("Host=localhost;Database=wcdb;Username=postgres;Password=postgres")
            .Options;
        return new WcDbContext(options);
    }
}
