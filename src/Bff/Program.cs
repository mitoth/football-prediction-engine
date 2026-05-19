using System.Security.Claims;
using WcPredictions.Bff;
using WcPredictions.Data;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddClerkAuth();

// Aspire Npgsql EF client — connection name matches the AppHost "wcdb" database.
builder.AddNpgsqlDbContext<WcDbContext>("wcdb");
builder.Services.AddHostedService<DbMigrator>();

// The Vite SPA is a separate origin (Aspire assigns it its own port), so the
// browser needs CORS to reach the BFF. Anonymous reads send no credentials,
// so any-origin is safe here; tighten when authed endpoints land in Phase 4.
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

// Phase 0 acceptance endpoint: 200 with a valid Clerk JWT, 401 without.
app.MapGet("/me", (ClaimsPrincipal user) =>
        Results.Ok(new { sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub") }))
    .RequireAuthorization();

app.Run();

// Exposed so the Phase 3 integration test can boot the real BFF via
// WebApplicationFactory<Program> with a Testcontainers Postgres.
public partial class Program;

