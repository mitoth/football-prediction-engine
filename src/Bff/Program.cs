using System.Security.Claims;
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
builder.Services.AddHttpClient<PredictionEngineClient>(c =>
    c.BaseAddress = new Uri("https+http://prediction-engine"));

// The Vite SPA is a separate origin (Aspire assigns it its own port), so the
// browser needs CORS to reach the BFF. Auth is a bearer JWT (not cookies), so
// any-origin stays safe — no AllowCredentials, nothing rides ambient session.
const string WebCors = "web";
builder.Services.AddCors(o => o.AddPolicy(WebCors, p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseCors(WebCors);
app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();

app.MapGet("/", () => "WcPredictions BFF");

// Phase 3: anonymous match list + match detail (baseline + citations).
app.MapMatchEndpoints();

// Phase 4: authed refinement hook (quota-gated POST, free PUT/DELETE, /me).
app.MapRefineEndpoints();

// Phase 0 acceptance endpoint: 200 with a valid Clerk JWT, 401 without.
app.MapGet("/me", (ClaimsPrincipal user) =>
        Results.Ok(new { sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub") }))
    .RequireAuthorization();

app.Run();

// Exposed so the Phase 3 integration test can boot the real BFF via
// WebApplicationFactory<Program> with a Testcontainers Postgres.
public partial class Program;

