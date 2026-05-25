# MatchForecast — Football Prediction App

Baseline AI match predictions that visibly react to what the user feeds them.
Launch target: **FIFA World Cup 2026** (opens 11 June 2026). Full architecture,
data model, and phased plan in [`football-prediction-app-design.html`](football-prediction-app-design.html).

---

## Running the app locally

Everything is orchestrated by .NET Aspire. One command brings up Postgres,
Redis, the five .NET services, the Vite frontend, and the Aspire dashboard.

### 1. Prerequisites (once per machine)

| Tool | Version | Notes |
| --- | --- | --- |
| .NET SDK | **10.0.x** | Aspire AppHost is `net10.0`; the services are `net9.0`. The 9.0 SDK is also installed transitively. |
| Aspire CLI | **13.3.x** | `dotnet tool install -g aspire.cli` (or update with `aspire update`). |
| Docker Desktop | running | Postgres + Redis containers and Testcontainers in tests. Must be **started before** `aspire run` / tests. |
| Node | 20.x | Frontend (`frontend/`). Installed automatically by the Aspire `web-installer` resource on first start. |
| (optional) .NET HTTPS dev cert | trusted | `dotnet dev-certs https --trust` — accept the Windows dialog. Without this the browser blocks the BFF cert and the match list shows `Failed to fetch`. One-time setup. |

### 2. Start the stack

```powershell
aspire run --project WcPredictions.AppHost\WcPredictions.AppHost.csproj --detach
```

What this does:

- Boots **postgres** + **cache (Redis)** containers (persistent — data survives restarts).
- Runs the `DbMigrator` hosted service on `Ingestion` startup → applies every EF migration in `src/Data/Migrations/` to `wcdb`.
- Starts **llm-gateway, prediction-engine, ingestion, bff** (.NET projects, all `net9.0`).
- Starts **web** (Vite + React 18 frontend on `npm run dev`).
- Prints a dashboard URL with a one-time browser token — open it to see every resource, its logs, traces, metrics, and a live env vars panel.

Aspire stops the previous AppHost automatically if one is already running.

### 3. Open it

When `aspire run` finishes, it prints something like:

```
   Dashboard:  https://localhost:17122/login?t=<token>
```

- **Aspire dashboard** (services + logs + traces) → that URL.
- **Web app** → the `web` resource on the dashboard shows the http URL (typically `http://localhost:611xx`). The port shifts on restart.
- **BFF** → `https://localhost:7240` (`/healthz`, `/matches`, etc.). The frontend already knows it via the Aspire env var `VITE_API_URL`.

### 4. Stop the stack

```powershell
aspire stop --project WcPredictions.AppHost\WcPredictions.AppHost.csproj
```

Containers stay (persistent lifetime, so the seeded fixtures + news survive). To
wipe state entirely: stop the AppHost, then `docker volume rm` the
`wcpredictions.apphost-*-postgres-data` volume.

---

## Secrets (real keys vs. running blind)

The AppHost reads keys from **AppHost user-secrets** so they never go in git.
Without them the services degrade gracefully (Ingestion logs a warning, Gateway
returns 401 on the first refinement, Clerk is anonymous-only).

Set keys with `dotnet user-secrets` against the AppHost project:

```powershell
$proj = "WcPredictions.AppHost"
dotnet user-secrets set "Parameters:anthropic-api-key"      "sk-ant-..."   --project $proj
dotnet user-secrets set "Parameters:api-football-key"       "..."          --project $proj
dotnet user-secrets set "Parameters:news-data-key"          "pub_..."      --project $proj
dotnet user-secrets set "Parameters:clerk-authority"        "https://<tenant>.clerk.accounts.dev" --project $proj
dotnet user-secrets set "Parameters:clerk-publishable-key"  "pk_test_..."  --project $proj
dotnet user-secrets set "Parameters:clerk-secret-key"       "sk_test_..."  --project $proj
```

Restart the AppHost after setting keys. **Do not** use the literal-value
`AddParameter("name", "value")` overload in `AppHost.cs` for anything other than
true constants — it silently pins the value and ignores user-secrets (`Cfg(name)`
helper is the right path).

---

## Tests

```powershell
dotnet test WcPredictions.sln                # all .NET tests (xUnit + Testcontainers)
cd frontend ; npm run test:e2e               # Playwright e2e (mocked BFF)
```

Docker Desktop must be running for Testcontainers (Postgres/Redis) and the
Ingestion integration test. The Playwright suite stubs the BFF over
`page.route` and a synthetic `window.__E2E_TOKEN__`, so it has **no** Docker or
network dependency.

---

## Common operations

| You want to | Do this |
| --- | --- |
| See live console output of one service | Aspire dashboard → resource → Console Logs tab |
| Re-apply EF migrations | Restart `ingestion` (DbMigrator runs on startup) |
| Add a migration | `dotnet ef migrations add <Name> --project src/Data --startup-project src/Data` then **rebuild Data** before running again (else the snapshot lags one build behind) |
| Rebuild + restart a single service after a code change | Aspire dashboard → resource → `rebuild` command (re-compiles and bounces the resource) |
| Force a fresh fixture sync | `restart` the `ingestion` resource — Quartz `StartNow` re-fires `FixtureSyncJob` immediately |
| Clear baselines so they regenerate with the latest prompt | `docker exec <postgres-container> psql -U postgres -d wcdb -c 'TRUNCATE "Baselines","BaselineCitations","PredictionSnapshots" CASCADE;'` then restart `prediction-engine` |
| Find the dynamic Postgres port | `docker ps --filter "name=postgres" --format "{{.Ports}}"` |
| Open the design doc in a browser | `Start-Process football-prediction-app-design.html` |

---

## Project layout

```
WcPredictions.sln
WcPredictions.AppHost/                 Aspire orchestration (net10.0)
src/
  Bff/                                 ASP.NET Minimal API (anonymous reads + authed refine)
  PredictionEngine/                    Baseline + refinement compute (Web SDK + Quartz)
  LlmGateway/                          Anthropic SDK choke point (claude-opus-4-7)
  Ingestion/                           API-Football + NewsData.io workers
  Data/                                EF Core entities + migrations + DbMigrator
  ServiceDefaults/                     OTel + service discovery + health
frontend/                              Vite 5 + React 18 + TypeScript SPA
tests/
  WcPredictions.Bff.Tests/             WebApplicationFactory + Testcontainers PG
  WcPredictions.Ingestion.Tests/       Testcontainers PG + WireMock API-Football / NewsData
  WcPredictions.PredictionEngine.Tests Testcontainers PG + Redis + WireMock gateway
football-prediction-app-design.html    Living design doc (read this first)
```

---

## When something breaks

- **`Failed to fetch` in the browser, match list empty.** .NET HTTPS dev cert isn't trusted. `dotnet dev-certs https --trust` and accept the dialog.
- **`aspire run` exits with `exit 7 "no AppHost project files detected"`.** Run from the repo root, or pass `--project WcPredictions.AppHost\WcPredictions.AppHost.csproj`.
- **EF runtime `FileNotFoundException` / DbMigrator crash on startup.** Version skew between the Aspire Npgsql EF client (pulls EF Core Relational 9.0.1) and `src/Data` (9.0.16). Any service that uses both needs explicit `Microsoft.EntityFrameworkCore` + `.Relational` refs at 9.0.16 to beat the transitive — see existing csprojs.
- **`column m.Stage does not exist` on Ingestion startup.** Quartz `StartNow` fired before DbMigrator finished applying a new migration. Restart the `ingestion` resource and it self-heals.
- **Build fails with `MSB3027: file locked` errors.** Aspire-spawned processes still hold the DLLs. `aspire stop` first, then `dotnet build`.
- **`Parameters:<name>` set in user-secrets but a service still sees empty.** Check `AppHost.cs`: every user-supplied parameter must read via the `Cfg(name)` helper, not the `AddParameter("name", "<literal>")` overload (which pins the value).

The full ops log lives in [`MEMORY.md`](../../.claude/projects/c--MyCodingProjects-WC-AI-Predictions-v2/memory/MEMORY.md) (per-developer machine memory, not in git).
