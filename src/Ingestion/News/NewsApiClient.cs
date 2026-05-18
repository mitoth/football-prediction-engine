using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace WcPredictions.Ingestion.News;

public sealed class NewsApiOptions
{
    public const string Section = "NewsApi";
    public string BaseUrl { get; set; } = "https://newsapi.org";
    public string ApiKey { get; set; } = "";
    public int PageSize { get; set; } = 50;
    // One fetch per query. Defaults track the v1 launch competitions.
    public List<string> Queries { get; set; } =
        ["FIFA World Cup 2026", "Premier League", "Champions League"];
}

public sealed record NewsArticle(
    string Headline, string Outlet, string Url, string Snippet, DateTimeOffset? PublishedAt);

public sealed class NewsApiClient(HttpClient http)
{
    // /v2/everything sorted by recency. Returns metadata only — never full text
    // (copyright; design §5).
    public async Task<IReadOnlyList<NewsArticle>> SearchAsync(
        string query, int pageSize, CancellationToken ct)
    {
        var url = $"/v2/everything?q={Uri.EscapeDataString(query)}" +
                  $"&language=en&sortBy=publishedAt&pageSize={pageSize}";
        var resp = await http.GetFromJsonAsync<NewsResponse>(url, ct);
        return resp?.Articles
            .Where(a => !string.IsNullOrWhiteSpace(a.Url))
            .Select(a => new NewsArticle(
                a.Title ?? "(untitled)",
                a.Source?.Name ?? "unknown",
                a.Url!,
                Trim(a.Description, 500),
                a.PublishedAt))
            .ToList() ?? [];
    }

    private static string Trim(string? s, int max) =>
        string.IsNullOrEmpty(s) ? "" : s.Length <= max ? s : s[..max];

    private sealed class NewsResponse
    {
        [JsonPropertyName("articles")] public List<ArticleDto> Articles { get; set; } = [];
    }
    private sealed class ArticleDto
    {
        public SourceDto? Source { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Url { get; set; }
        public DateTimeOffset? PublishedAt { get; set; }
    }
    private sealed class SourceDto { public string? Name { get; set; } }
}
