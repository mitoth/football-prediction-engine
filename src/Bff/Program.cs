using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using WcPredictions.Bff;
using WcPredictions.Data;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddClerkAuth();

// Aspire Npgsql EF client — connection name matches the AppHost "wcdb" database.
builder.AddNpgsqlDbContext<WcDbContext>("wcdb");
builder.Services.AddHostedService<DbMigrator>();

// Refinement orchestration: the per-request Clerk identity, the daily quota,
// and a typed client to the Prediction Engine. URL refinements were removed
// (§4, §5 of the design doc — publisher commercial-reuse exposure), so the
// previously sandboxed URL Fetcher service and its client are gone.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped(sp => new CurrentUser(
    sp.GetRequiredService<IHttpContextAccessor>().HttpContext!.User,
    sp.GetRequiredService<WcDbContext>()));
builder.Services.AddScoped<QuotaService>();
// /refine wraps a Claude call (~20-40s). The LLM-grade resilience timeout lives
// in ServiceDefaults (global); the long HttpClient.Timeout just keeps the outer
// SendAsync ceiling clear of it.
builder.Services.AddHttpClient<PredictionEngineClient>(c =>
{
    c.BaseAddress = new Uri("https+http://prediction-engine");
    c.Timeout = TimeSpan.FromMinutes(3);
});

// The Vite SPA is a separate origin (Aspire assigns it its own port), so the
// browser needs CORS to reach the BFF. Chat mode rides on the mf_anon_id
// cookie for unsigned-in users, which forces `credentials: 'include'` on the
// fetch — and browsers refuse the wildcard `Access-Control-Allow-Origin: *`
// with credentials. Switch to an explicit allowlist + AllowCredentials so the
// SPA can send cookies; non-credentialed reads (anonymous /matches, /me probe)
// still work from these origins.
const string WebCors = "web";
var corsOrigins = (builder.Configuration["Cors:AllowedOrigins"]
        ?? "https://wcaipredictions.com,https://www.wcaipredictions.com,http://localhost:5173")
    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
builder.Services.AddCors(o => o.AddPolicy(WebCors, p =>
    p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

// Per-IP fixed-window limit on the refinement POST endpoint. The per-user
// quota in QuotaService already enforces the 3-a-day business rule; this is
// a defence-in-depth against burst abuse before a request even reaches the
// LLM gateway. Conservative limit: 10 requests / minute / IP, which is well
// above any realistic human pattern (3-credit free + a few rejected
// gibberish) and 1-2 orders of magnitude below what would matter for cost.
const string RefinePolicy = "refine-per-ip";
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(RefinePolicy, ctx =>
    {
        var key = ctx.Connection.RemoteIpAddress?.ToString() ?? "anon";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
        });
    });
});

var app = builder.Build();

app.UseCors(WebCors);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();

app.MapGet("/", () => "WcPredictions BFF");

// Phase 3: anonymous match list + match detail (baseline + citations).
app.MapMatchEndpoints();

// Phase 4: authed refinement hook (quota-gated POST, free PUT/DELETE, /me).
app.MapRefineEndpoints();

// Phase 4.5: chat-mode refinement (anonymous-friendly, multi-turn). New surface
// — keeps the legacy /refine endpoints around for one release.
app.MapChatEndpoints();

// GDPR Articles 15 + 17: data export + erasure for the authed user.
app.MapGdprEndpoints();

// Phase 0 acceptance endpoint: 200 with a valid Clerk JWT, 401 without.
app.MapGet("/me", (ClaimsPrincipal user) =>
        Results.Ok(new { sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub") }))
    .RequireAuthorization();

app.Run();

// Exposed so the Phase 3 integration test can boot the real BFF via
// WebApplicationFactory<Program> with a Testcontainers Postgres.
public partial class Program;

