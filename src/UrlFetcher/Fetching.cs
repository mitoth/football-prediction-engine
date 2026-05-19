using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace WcPredictions.UrlFetcher;

// Contract (BFF mirrors these). Status: "ok" | "dead_url" | "blocked".
// "blocked" = an SSRF/policy reject (bad scheme, private IP, redirect, too big);
// "dead_url" = the link is just unreachable/non-2xx. Callers spend no quota for
// either — the distinction is only for the user-facing message.
public sealed record FetchRequest(string Url);
public sealed record FetchResult(string Status, string? Text);

public static partial class UrlFetcherEndpoint
{
    private const int MaxBytes = 2 * 1024 * 1024;          // 2 MB hard cap
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public static void MapFetch(this WebApplication app)
    {
        app.MapPost("/fetch", async (FetchRequest req, IHttpClientFactory http, CancellationToken ct) =>
            Results.Ok(await FetchAsync(req.Url, http, ct)));
    }

    public static async Task<FetchResult> FetchAsync(
        string url, IHttpClientFactory httpFactory, CancellationToken ct)
    {
        // 1. Scheme + shape.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return new FetchResult("blocked", null);

        // 2. Resolve the host and reject if ANY resolved address is private —
        // closes DNS-rebinding / SSRF to internal services and cloud metadata.
        IPAddress[] addrs;
        try { addrs = await Dns.GetHostAddressesAsync(uri.Host, ct); }
        catch { return new FetchResult("dead_url", null); }
        if (addrs.Length == 0 || addrs.Any(IsPrivate))
            return new FetchResult("blocked", null);

        // 3. Fetch with redirects OFF (a 30x to an internal host is the classic
        // bypass), a hard timeout, and a streamed size cap.
        var client = httpFactory.CreateClient("fetch");
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(Timeout);
        try
        {
            using var resp = await client.GetAsync(
                uri, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            if ((int)resp.StatusCode is < 200 or >= 300)
                return new FetchResult("dead_url", null);

            await using var stream = await resp.Content.ReadAsStreamAsync(cts.Token);
            var buffer = new byte[MaxBytes];
            int total = 0, read;
            while (total < MaxBytes &&
                   (read = await stream.ReadAsync(buffer.AsMemory(total, MaxBytes - total), cts.Token)) > 0)
                total += read;

            var html = Encoding.UTF8.GetString(buffer, 0, total);
            var text = ExtractReadableText(html);
            return text.Length == 0
                ? new FetchResult("dead_url", null)
                : new FetchResult("ok", text);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new FetchResult("dead_url", null); // our 5s timeout
        }
        catch (HttpRequestException)
        {
            return new FetchResult("dead_url", null);
        }
    }

    private static bool IsPrivate(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return true;
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            return b[0] == 10                                   // 10/8
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)    // 172.16/12
                || (b[0] == 192 && b[1] == 168)                 // 192.168/16
                || (b[0] == 169 && b[1] == 254)                 // 169.254/16 link-local (cloud metadata)
                || b[0] == 0 || b[0] >= 224;                    // 0.0.0.0/8, multicast/reserved
        }
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6Multicast || ip.IsIPv6SiteLocal) return true;
            var b = ip.GetAddressBytes();
            return (b[0] & 0xFE) == 0xFC;                        // fc00::/7 unique-local
        }
        return true; // unknown family → deny
    }

    // Minimal readability: drop script/style/noscript, strip tags, decode
    // entities, collapse whitespace. Good enough for a news-article extract in
    // v1 (no extra dependency); the gateway already quarantines it as untrusted.
    private static string ExtractReadableText(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";
        var s = ScriptStyle().Replace(html, " ");
        s = Tags().Replace(s, " ");
        s = WebUtility.HtmlDecode(s);
        s = Whitespace().Replace(s, " ").Trim();
        return s.Length > 8000 ? s[..8000] : s; // cap context size
    }

    [GeneratedRegex(@"<(script|style|noscript)[^>]*>.*?</\1>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptStyle();
    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();
    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
