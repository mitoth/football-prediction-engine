using WcPredictions.UrlFetcher;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Redirects OFF: a 30x to an internal host is the classic SSRF bypass, so the
// guard in Fetching.cs only validates the URL the user actually gave us.
builder.Services.AddHttpClient("fetch")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = false,
    });

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapGet("/", () => "WcPredictions URL Fetcher");
app.MapFetch();

app.Run();

// Exposed for the SSRF unit tests.
public partial class Program;
