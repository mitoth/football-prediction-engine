var builder = DistributedApplication.CreateBuilder(args);

// --- Infrastructure ---------------------------------------------------------
// Persistent container lifetime + data volume so local data survives AppHost
// restarts (Phase 0 dev experience).
var postgres = builder.AddPostgres("postgres")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume();

var wcdb = postgres.AddDatabase("wcdb");

var cache = builder.AddRedis("cache")
    .WithLifetime(ContainerLifetime.Persistent);

// --- Clerk auth config ------------------------------------------------------
// Authority is the Clerk issuer (set per-environment once the tenant exists).
// The dev signing key is Development-only: it lets Phase 0 tests mint a valid
// JWT against the BFF /me endpoint with no real Clerk tenant. Not a real secret.
var clerkAuthority = builder.AddParameter("clerk-authority", "", secret: false);
var clerkDevSigningKey = builder.AddParameter(
    "clerk-dev-signing-key", "phase0-dev-only-signing-key-change-me!", secret: false);
// Clerk frontend publishable key (empty in Phase 0 — shell renders without it).
var clerkPublishableKey = builder.AddParameter("clerk-publishable-key", "", secret: false);

// --- Ingestion API keys -----------------------------------------------------
// Secret, empty default so `aspire start` doesn't prompt when running without
// live keys. Ingestion degrades gracefully (logs, no data) until the user sets
// real keys; integration tests stub the upstream APIs.
var apiFootballKey = builder.AddParameter("api-football-key", "", secret: true);
var newsApiKey = builder.AddParameter("news-api-key", "", secret: true);

// --- Services ---------------------------------------------------------------

var llmGateway = builder.AddProject<Projects.WcPredictions_LlmGateway>("llm-gateway");

var urlFetcher = builder.AddProject<Projects.WcPredictions_UrlFetcher>("url-fetcher");

var predictionEngine = builder.AddProject<Projects.WcPredictions_PredictionEngine>("prediction-engine")
    .WithReference(wcdb)
    .WithReference(cache)
    .WithReference(llmGateway)
    .WaitFor(wcdb)
    .WaitFor(llmGateway);

var ingestion = builder.AddProject<Projects.WcPredictions_Ingestion>("ingestion")
    .WithReference(wcdb)
    .WithEnvironment("ApiFootball__ApiKey", apiFootballKey)
    .WithEnvironment("NewsApi__ApiKey", newsApiKey)
    .WaitFor(wcdb);

var bff = builder.AddProject<Projects.WcPredictions_Bff>("bff")
    .WithReference(wcdb)
    .WithReference(cache)
    .WithReference(predictionEngine)
    .WithReference(urlFetcher)
    .WithEnvironment("Clerk__Authority", clerkAuthority)
    .WithEnvironment("Clerk__DevSigningKey", clerkDevSigningKey)
    .WaitFor(wcdb)
    .WaitFor(predictionEngine);

// --- Frontend ---------------------------------------------------------------
// Vite React SPA. API base + Clerk key passed via env; the BFF URL is an
// endpoint reference (never hardcoded) so it survives Aspire port assignment.
builder.AddViteApp("web", "../frontend")
    .WithExternalHttpEndpoints()
    .WithReference(bff)
    .WithEnvironment("VITE_API_URL", bff.GetEndpoint("https"))
    .WithEnvironment("VITE_CLERK_PUBLISHABLE_KEY", clerkPublishableKey)
    .WaitFor(bff);

builder.Build().Run();
