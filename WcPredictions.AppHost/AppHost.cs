var builder = DistributedApplication.CreateBuilder(args);

// --- Infrastructure ---------------------------------------------------------
// Local dev: spins up a Postgres container with persistent volume (data
// survives AppHost restarts). Cloud publish (`azd up`): provisions Azure
// Database for PostgreSQL Flexible Server. PublishAsAzurePostgresFlexibleServer
// flips the publish target without breaking the local container path.
//
// Switched off `AddPostgres` + `WithDataVolume()` because Container Apps
// mounts Azure Files via SMB, and SMB does not implement POSIX chmod —
// Postgres' initdb refuses to start on a directory it cannot lock down
// ("could not change permissions of directory /var/lib/postgresql/data:
// Operation not permitted"). Managed PostgreSQL sidesteps the whole
// platform-permission class of bugs and unlocks point-in-time restore.
// Force password auth on the managed Flexible Server. The Aspire default is
// Entra-only, which requires every client to acquire a managed-identity token
// — the stock `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` client integration
// in our services has no token provider wired, so the first migration fails
// with "No password has been provided but the backend requires one (in
// SASL/SCRAM-SHA-256-PLUS)". Password auth keeps the connection string
// self-contained; Aspire auto-generates and stores the admin password as a
// secret parameter referenced from Container App secret slots.
var postgres = builder.AddAzurePostgresFlexibleServer("postgres")
    .WithPasswordAuthentication()
    .RunAsContainer(c => c
        .WithLifetime(ContainerLifetime.Persistent)
        .WithDataVolume());

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
// integration tests stub the upstreams. News is curated football RSS (no key).
var apiFootballKey = builder.AddParameter("api-football-key", Cfg("api-football-key"), secret: true);
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
    .WaitFor(wcdb);

var bff = builder.AddProject<Projects.WcPredictions_Bff>("bff")
    .WithReference(wcdb)
    .WithReference(cache)
    .WithReference(predictionEngine)
    .WithEnvironment("Clerk__Authority", clerkAuthority)
    .WithEnvironment("Clerk__DevSigningKey", clerkDevSigningKey)
    .WithEnvironment("Clerk__SecretKey", clerkSecretKey)
    // Publicly reachable so the Vite SPA on Azure Static Web Apps can hit it.
    // All other services (Ingestion, PredictionEngine, LlmGateway) stay
    // .internal — only the BFF is the public entry point.
    .WithExternalHttpEndpoints()
    .WaitFor(wcdb)
    .WaitFor(predictionEngine);

// --- Frontend ---------------------------------------------------------------
// Vite React SPA. API base + Clerk key passed via env; the BFF URL is an
// endpoint reference (never hardcoded) so it survives Aspire port assignment.
//
// PRODUCTION DEPLOY NOTE: ExcludeFromManifest keeps this resource out of the
// Aspire→Azure publish manifest. The SPA ships separately to Azure Static Web
// Apps (free tier) via .github/workflows/deploy.yml — see docs/azure-deploy.md.
// In Container Apps the Vite dev server would idle a paid replica for no gain;
// SWA gives global CDN + free TLS at $0.
builder.AddViteApp("web", "../frontend")
    .WithExternalHttpEndpoints()
    .WithReference(bff)
    .WithEnvironment("VITE_API_URL", bff.GetEndpoint("https"))
    .WithEnvironment("VITE_CLERK_PUBLISHABLE_KEY", clerkPublishableKey)
    .WaitFor(bff)
    .ExcludeFromManifest();

builder.Build().Run();
