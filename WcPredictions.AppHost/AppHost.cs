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

// Parameter values come from configuration (`Parameters:<name>` — AppHost
// user-secrets in dev / env in prod), with an empty fallback so `aspire start`
// never prompts when a key is unset. NOTE: `AddParameter(name, "<literal>")`
// pins the value and ignores configuration — that overload is only correct for
// a true constant (the dev signing key). Everything user-supplied must read
// configuration here, or the secret store is silently never consulted.
string Cfg(string name, string fallback = "") =>
    builder.Configuration[$"Parameters:{name}"] ?? fallback;

// --- Clerk auth config ------------------------------------------------------
// Authority is the Clerk issuer. The dev signing key is Development-only: it
// lets tests mint a valid JWT against the BFF with no real Clerk tenant — a
// genuine constant, so the literal-value overload is correct for it alone.
var clerkAuthority = builder.AddParameter("clerk-authority", Cfg("clerk-authority"), secret: false);
var clerkDevSigningKey = builder.AddParameter(
    "clerk-dev-signing-key", "phase0-dev-only-signing-key-change-me!", secret: false);
var clerkPublishableKey = builder.AddParameter("clerk-publishable-key", Cfg("clerk-publishable-key"), secret: false);
// Clerk backend secret key. Unused until Phase 5 (the Stripe webhook writes the
// new tier into Clerk publicMetadata via the Clerk backend API).
var clerkSecretKey = builder.AddParameter("clerk-secret-key", Cfg("clerk-secret-key"), secret: true);

// --- Ingestion / model API keys ---------------------------------------------
// Empty fallback ⇒ no prompt when unset; services degrade gracefully and the
// integration tests stub the upstreams.
var apiFootballKey = builder.AddParameter("api-football-key", Cfg("api-football-key"), secret: true);
var newsDataKey = builder.AddParameter("news-data-key", Cfg("news-data-key"), secret: true);
var anthropicKey = builder.AddParameter("anthropic-api-key", Cfg("anthropic-api-key"), secret: true);

// --- Services ---------------------------------------------------------------

var llmGateway = builder.AddProject<Projects.WcPredictions_LlmGateway>("llm-gateway")
    .WithEnvironment("ANTHROPIC_API_KEY", anthropicKey);

var predictionEngine = builder.AddProject<Projects.WcPredictions_PredictionEngine>("prediction-engine")
    .WithReference(wcdb)
    .WithReference(cache)
    .WithReference(llmGateway)
    .WaitFor(wcdb)
    .WaitFor(llmGateway);

var ingestion = builder.AddProject<Projects.WcPredictions_Ingestion>("ingestion")
    .WithReference(wcdb)
    .WithEnvironment("ApiFootball__ApiKey", apiFootballKey)
    .WithEnvironment("News__ApiKey", newsDataKey)
    .WaitFor(wcdb);

var bff = builder.AddProject<Projects.WcPredictions_Bff>("bff")
    .WithReference(wcdb)
    .WithReference(cache)
    .WithReference(predictionEngine)
    .WithEnvironment("Clerk__Authority", clerkAuthority)
    .WithEnvironment("Clerk__DevSigningKey", clerkDevSigningKey)
    .WithEnvironment("Clerk__SecretKey", clerkSecretKey)
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
