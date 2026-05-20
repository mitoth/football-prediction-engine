using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace WcPredictions.Ingestion.News;

// NewsData.io is a commercial-licensed news API: legal coverage for a paid
// product (curated RSS isn't — most outlet ToS restrict commercial reuse).
// We use /news with category=sports + a football query; metadata only per §5.
public sealed class NewsDataOptions
{
    public const string Section = "News";
    public string BaseUrl { get; set; } = "https://newsdata.io/api/1";
    public string ApiKey { get; set; } = "";
    // Single string per request; multiple queries → multiple paged calls.
    public List<string> Queries { get; set; } = ["football OR soccer"];
    public string Language { get; set; } = "en";
    public string Category { get; set; } = "sports";
    public int PageSize { get; set; } = 50;
}

public sealed record NewsArticle(
    string Headline, string Outlet, string Url, string Snippet, DateTimeOffset? PublishedAt);

public sealed class NewsDataClient(HttpClient http, ILogger<NewsDataClient> log)
{
    public async Task<IReadOnlyList<NewsArticle>> SearchAsync(
        string query, NewsDataOptions opt, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(opt.ApiKey))
        {
            log.LogWarning("NewsData.io api key not set — skipping query '{Query}'", query);
            return [];
        }

        var url = $"/news?apikey={Uri.EscapeDataString(opt.ApiKey)}" +
                  $"&q={Uri.EscapeDataString(query)}" +
                  $"&language={opt.Language}" +
                  $"&category={opt.Category}" +
                  $"&size={opt.PageSize}";

        try
        {
            var resp = await http.GetFromJsonAsync<NewsDataResponse>(url, ct);
            return resp?.Results
                .Where(r => !string.IsNullOrWhiteSpace(r.Link))
                .Select(r => new NewsArticle(
                    string.IsNullOrWhiteSpace(r.Title) ? "(untitled)" : r.Title!.Trim(),
                    r.SourceName ?? r.SourceId ?? "unknown",
                    r.Link!,
                    Trim(r.Description, 500),
                    ParsePubDate(r.PubDate)))
                .ToList() ?? [];
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "NewsData.io query '{Query}' failed", query);
            return [];
        }
    }

    private static string Trim(string? s, int max) =>
        string.IsNullOrEmpty(s) ? "" : s.Length <= max ? s : s[..max];

    // NewsData.io pubDate is "YYYY-MM-DD HH:MM:SS" (UTC). Npgsql refuses any
    // DateTimeOffset whose offset != 0 for timestamptz, so we always emit UTC.
    private static DateTimeOffset? ParsePubDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal |
            System.Globalization.DateTimeStyles.AdjustToUniversal, out var d)
            ? new DateTimeOffset(DateTime.SpecifyKind(d, DateTimeKind.Utc), TimeSpan.Zero)
            : null;
    }

    private sealed class NewsDataResponse
    {
        [JsonPropertyName("results")] public List<Result> Results { get; set; } = [];
    }

    private sealed class Result
    {
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("link")] public string? Link { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("source_id")] public string? SourceId { get; set; }
        [JsonPropertyName("source_name")] public string? SourceName { get; set; }
        [JsonPropertyName("pubDate")] public string? PubDate { get; set; }
    }
}
