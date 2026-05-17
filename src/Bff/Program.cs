using System.Security.Claims;
using WcPredictions.Bff;
using WcPredictions.Data;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddClerkAuth();

// Aspire Npgsql EF client — connection name matches the AppHost "wcdb" database.
builder.AddNpgsqlDbContext<WcDbContext>("wcdb");
builder.Services.AddHostedService<DbMigrator>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();

app.MapGet("/", () => "WcPredictions BFF");

// Phase 0 acceptance endpoint: 200 with a valid Clerk JWT, 401 without.
app.MapGet("/me", (ClaimsPrincipal user) =>
        Results.Ok(new { sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub") }))
    .RequireAuthorization();

app.Run();

