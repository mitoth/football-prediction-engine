using System.Net;
using System.Text.RegularExpressions;
using System.ServiceModel.Syndication;
using System.Xml;

namespace WcPredictions.Ingestion.News;

// Curated football RSS replaces a general news API: football-focused by
// construction, free, no key, real-time, no rate limit (design §5 wants
// metadata-only ranked snippets deduped by URL — RSS fits exactly). The set is
// server-curated, so no SSRF surface (unlike user-pasted links).
public sealed class RssFeed
{
    public string Url { get; set; } = "";
    public string Outlet { get; set; } = "";
}

public sealed class RssOptions
{
    public const string Section = "News";
    public List<RssFeed> Feeds { get; set; } =
    [
        new() { Url = "https://feeds.bbci.co.uk/sport/football/rss.xml", Outlet = "BBC Sport" },
        new() { Url = "https://www.theguardian.com/football/rss",        Outlet = "The Guardian" },
        new() { Url = "https://www.skysports.com/rss/12040",             Outlet = "Sky Sports" },
        new() { Url = "https://www.espn.com/espn/rss/soccer/news",       Outlet = "ESPN" },
    ];
    public int MaxItemsPerFeed { get; set; } = 50;
}

public sealed record NewsArticle(
    string Headline, string Outlet, string Url, string Snippet, DateTimeOffset? PublishedAt);

public sealed partial class FootballRssClient(HttpClient http, ILogger<FootballRssClient> log)
{
    public async Task<IReadOnlyList<NewsArticle>> FetchAsync(
        RssFeed feed, int maxItems, CancellationToken ct)
    {
        try
        {
            await using var stream = await http.GetStreamAsync(feed.Url, ct);
            // Prohibit DTDs / external entities — defensive even for curated feeds.
            using var xml = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                Async = true,
            });
            var syndication = SyndicationFeed.Load(xml);
            if (syndication is null) return [];

            return syndication.Items
                .Take(maxItems)
                .Select(i => new NewsArticle(
                    string.IsNullOrWhiteSpace(i.Title?.Text) ? "(untitled)" : i.Title.Text.Trim(),
                    feed.Outlet,
                    i.Links.FirstOrDefault(l => l.Uri?.IsAbsoluteUri == true)?.Uri.ToString()
                        ?? i.Id ?? "",
                    Clean(i.Summary?.Text),
                    SafePublishedAtUtc(i)))
                .Where(a => !string.IsNullOrWhiteSpace(a.Url))
                .ToList();
        }
        catch (Exception ex)
        {
            // One bad feed must not fail the whole sync.
            log.LogWarning(ex, "RSS feed {Feed} failed", feed.Url);
            return [];
        }
    }

    // SyndicationItem.PublishDate parses on access and throws on wonky pubDate
    // formats (e.g. Sky Sports); skip per-item rather than killing the feed.
    // Npgsql refuses any DateTimeOffset with offset != 0 for timestamptz, so
    // normalize to UTC (ESPN feed pubDates are -05:00 and would 100% roll back
    // the whole batch insert).
    private static DateTimeOffset? SafePublishedAtUtc(SyndicationItem item)
    {
        try
        {
            var p = item.PublishDate;
            if (p == default) return null;
            return p.ToUniversalTime();
        }
        catch { return null; }
    }

    // RSS summaries often carry HTML; keep metadata-only plain text (design §5).
    private static string Clean(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var text = WebUtility.HtmlDecode(Tags().Replace(s, " ")).Trim();
        text = Whitespace().Replace(text, " ");
        return text.Length > 500 ? text[..500] : text;
    }

    [GeneratedRegex("<[^>]+>")] private static partial Regex Tags();
    [GeneratedRegex(@"\s+")] private static partial Regex Whitespace();
}
