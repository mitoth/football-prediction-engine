using WcPredictions.UrlFetcher;

namespace WcPredictions.UrlFetcher.Tests;

// The URL Fetcher is the airlock for untrusted links. These assert the SSRF
// guard rejects the dangerous shapes BEFORE any outbound request — none of
// these cases reach HTTP, so no network/DNS to a live host is needed.
public class SsrfGuardTests
{
    // CreateClient is only hit on the allowed path (not exercised here), so a
    // bare factory is enough — the guard returns first for every case below.
    private sealed class StubFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private static Task<FetchResult> Fetch(string url) =>
        UrlFetcherEndpoint.FetchAsync(url, new StubFactory(), CancellationToken.None);

    [Theory]
    [InlineData("ftp://example.com/x")]        // non-http scheme
    [InlineData("file:///etc/passwd")]         // non-http scheme
    [InlineData("not-a-url")]                  // not absolute
    public async Task Bad_scheme_or_shape_is_blocked(string url) =>
        Assert.Equal("blocked", (await Fetch(url)).Status);

    [Theory]
    [InlineData("http://127.0.0.1/admin")]               // loopback
    [InlineData("http://10.1.2.3/")]                     // 10/8 private
    [InlineData("http://192.168.0.5/")]                  // 192.168/16 private
    [InlineData("http://169.254.169.254/latest/meta")]   // cloud metadata (link-local)
    [InlineData("http://[::1]/")]                        // IPv6 loopback
    public async Task Private_or_metadata_targets_are_blocked(string url) =>
        Assert.Equal("blocked", (await Fetch(url)).Status);

    [Fact]
    public async Task Unresolvable_host_is_dead_url() =>
        Assert.Equal("dead_url",
            (await Fetch("http://wc-predictions-nonexistent.invalid/a")).Status);
}
