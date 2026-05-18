using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WcPredictions.Data;
using WcPredictions.Ingestion.News;

namespace WcPredictions.Ingestion.Sync;

// Fetches news per configured query and upserts metadata-only Article rows,
// deduplicated by URL. Idempotent.
public sealed class NewsSyncService(
    NewsApiClient api,
    WcDbContext db,
    IOptions<NewsApiOptions> options,
    ILogger<NewsSyncService> log)
{
    public async Task SyncAsync(CancellationToken ct)
    {
        var opt = options.Value;
        var seen = await db.Articles.Select(a => a.Url).ToListAsync(ct);
        var known = new HashSet<string>(seen);
        var added = 0;

        foreach (var query in opt.Queries)
        {
            var articles = await api.SearchAsync(query, opt.PageSize, ct);
            foreach (var a in articles)
            {
                if (!known.Add(a.Url)) continue; // dedupe by URL (also unique-indexed)
                db.Articles.Add(new Article
                {
                    Id = Guid.NewGuid(),
                    Headline = a.Headline,
                    Outlet = a.Outlet,
                    Url = a.Url,
                    Snippet = a.Snippet,
                    PublishedAt = a.PublishedAt,
                    FetchedAt = DateTimeOffset.UtcNow,
                });
                added++;
            }
            log.LogInformation("News query '{Query}': {Count} fetched", query, articles.Count);
        }

        await db.SaveChangesAsync(ct);
        log.LogInformation("News sync added {Added} new articles", added);
    }
}
