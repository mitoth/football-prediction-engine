using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WcPredictions.Data;
using WcPredictions.Ingestion.News;

namespace WcPredictions.Ingestion.Sync;

// Pulls each curated football RSS feed and upserts metadata-only Article rows,
// deduplicated by URL (also unique-indexed). Idempotent: re-running adds only
// genuinely new items.
public sealed class NewsSyncService(
    FootballRssClient rss,
    WcDbContext db,
    IOptions<RssOptions> options,
    ILogger<NewsSyncService> log)
{
    public async Task SyncAsync(CancellationToken ct)
    {
        var opt = options.Value;
        var seen = await db.Articles.Select(a => a.Url).ToListAsync(ct);
        var known = new HashSet<string>(seen);
        var added = 0;

        foreach (var feed in opt.Feeds)
        {
            var articles = await rss.FetchAsync(feed, opt.MaxItemsPerFeed, ct);
            foreach (var a in articles)
            {
                if (!known.Add(a.Url)) continue; // dedupe by URL
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
            log.LogInformation("RSS feed '{Outlet}': {Count} items", feed.Outlet, articles.Count);
        }

        await db.SaveChangesAsync(ct);
        log.LogInformation("News sync added {Added} new articles", added);
    }
}
