# Launch prep — checklist

Target launch: **on or before 11 June 2026** (WC 2026 opens — design §16 hard
deadline). Today: 2026-05-25 (~17 days out).

This file tracks every legal / policy / trust item still needed for a paid
public launch. Tick the box when done; link the commit hash if the change is
code.

Buckets:

- **Vendor** — what each upstream provider requires of us
- **User-facing legal** — what our visitors and paying users need to see
- **Operational** — ops controls that protect us once payments start flowing

---

## Vendor

### NewsData.io (paid)

- [x] Read the live ToS — snapshot in [`newsdata-tos-snapshot-2026-05-25.md`](newsdata-tos-snapshot-2026-05-25.md)
- [x] API key kept private (AppHost user-secrets, never client-side)
- [x] Don't proxy the API to end users (we expose curated articles via BFF only)
- [x] Don't store full article text (only headline + outlet + URL + snippet)
- [x] Citation panel links out to publisher URL (`target="_blank" rel="noreferrer noopener"`)
- [x] Footer credit ("News headlines via NewsData.io") — `App.tsx` + `MatchDetail` citation block
- [x] Snippet length cap at 250 chars on display — `MatchDetail.trim()`
- [ ] Send the LLM-use confirmation email — draft in [`newsdata-llm-use-confirmation-email.md`](newsdata-llm-use-confirmation-email.md). Save the reply in this folder.

### API-Football (Pro)

- [x] Paid Pro plan (unlocks WC 2026 + current seasons)
- [x] Key kept private (AppHost user-secrets)
- [x] No proxy of the API to end users
- [x] Footer credit ("Fixture data via API-Football")
- [x] Logos / badges intentionally not rendered (only team names + league names + group letter as plain text). HARD RULE comment in `src/Ingestion/ApiFootball/ApiFootballClient.cs` documents this for future developers.
- [x] Code-side enforcement: no `logo`, `crest`, `badge`, or image URL ever read from the API-Football response (grep-verified across `src/` and `frontend/src/`).
- [x] **Verbatim ToS captured (partial)** — logos / images / trademarks clause is the highest-risk one and is in [`api-football-tos-snapshot.md`](api-football-tos-snapshot.md) verbatim. Verdict per hot-spot recorded.
- [ ] **Still needed (lower priority):** capture verbatim text of the commercial-use license clause, the rate-limit / cache clause, the AI/LLM use clause if any, and the mass-media / betting / fantasy carve-out from your copy of the live ToS. Append to the same snapshot.
- [ ] Optional: send the API-Sports support email mirroring the NewsData LLM-use email pattern.

### Anthropic (Claude API)

- [x] Key kept private (AppHost user-secrets)
- [x] Single choke point (LLM Gateway) — per design §5 / §16
- [x] AUP + Commercial Terms snapshotted — see [`anthropic-policy-snapshot-2026-05-25.md`](anthropic-policy-snapshot-2026-05-25.md)
- [x] AI-disclosure to users (AUP requires it) — global footer + match-detail eyebrow already do this
- [x] "May be false / misleading" caution — added to ToS page (§2)
- [x] Liability cap mirrors Anthropic's (ToS §10)
- [x] Anthropic listed as sub-processor in Privacy Policy

### Clerk (auth)

- [x] Real tenant wired (`organic-sawfish-70.clerk.accounts.dev`)
- [x] JWT auth backed by JWKS
- [ ] Production Clerk instance set up (currently dev tenant). New `clerk-authority` / publishable / secret keys for the production tier.
- [ ] Restrict allowed redirect origins on the Clerk tenant to the production domain
- [ ] Confirm Clerk's data-residency setting matches where our users are (EU vs. US — affects GDPR posture)

### Stripe (Phase 5 — not started yet)

- [ ] Test-mode integration of the $0.99 matchday pass + $5 WC tournament pass
- [ ] Webhook → Entitlement row → Clerk publicMetadata tier sync
- [ ] Refund + cancellation policy page (Stripe requires this be linked from Checkout)
- [ ] Tax / VAT handling — likely use Stripe Tax for first launch
- [ ] Receipt emails on
- [ ] Live-mode keys gated behind production Clerk tenant

---

## User-facing legal

- [x] "Entertainment, not betting advice" disclaimer on match detail
- [x] Per-page kickoff timestamp, "PRE-MATCH PREDICTION" eyebrow, "Predicted final score · not the actual result" caption — no ambiguity that the displayed score is a forecast
- [x] **Privacy Policy page** at `/privacy` — `frontend/src/pages/Privacy.tsx`; identifies sub-processors, retention, GDPR rights, age, security
- [x] **Terms of Service page** at `/terms` — `frontend/src/pages/Terms.tsx`; AI-may-be-wrong, entertainment-not-betting, free quota, paid passes, refunds, no-warranty, liability cap
- [x] **Cookie banner / consent** — `frontend/src/components/CookieBanner.tsx`; informs about Clerk session cookie only, no analytics today
- [x] **GDPR data-export endpoint** — `GET /me/export` in `src/Bff/GdprEndpoints.cs` (authed; returns full user JSON dump; stamps `ExportRequestedAt` audit trail)
- [x] **GDPR data-delete endpoint** — `DELETE /me` in `src/Bff/GdprEndpoints.cs` (authed; cascades through snapshots → refinements → quota → entitlements → user; tells the user to also delete Clerk + handle Stripe)
- [x] **Contact email pattern** — `privacy@`, `billing@`, `legal@`, `hello@matchforecast.app` referenced in Privacy + ToS + footer. Provision the inbox once the domain is registered.
- [ ] **DMCA designated agent** — pick an address (suggest `legal@matchforecast.app`), register it with the US Copyright Office's online directory when the operating entity is incorporated (statutory safe-harbour requires this for hosted user content like refinement notes).
- [ ] **Footer links to Privacy + Terms + Contact** rendered globally ✅ — confirm visible on every route once styling lands.
- [ ] **Legal review by a real lawyer** before paid Stripe traffic. Privacy + ToS are working drafts authored by the developer.

---

## Operational

- [ ] Production domain registered + DNS pointed at hosting target
- [ ] TLS cert from a public CA (Let's Encrypt / managed by Azure Container Apps)
- [ ] Production Postgres backup schedule (daily snapshots, 7-day retention minimum)
- [ ] Production Redis (Aspire spins one for dev; production needs a managed instance or `WithDataVolume` on a persistent backing store — Phase 0 §16 acceptance)
- [ ] Cost alerts (Claude API spend, NewsData credit usage, API-Football credit usage) — Aspire dashboard surfaces tokens; add a daily ceiling alert
- [ ] Health-check + uptime monitor (Aspire `MapDefaultEndpoints` provides `/health` and `/alive`)
- [ ] Error reporting hooked to an off-host store (OTel → an OTLP backend)
- [x] **Rate limiting on `/matches/*/refine` POST per IP** — `src/Bff/Program.cs` adds an ASP.NET fixed-window limiter (10 req / 1 min / IP) on POST + PUT; per-user quota still in `QuotaService` for the business rule
- [ ] Load-test the cached baseline path under WC group-stage concurrent kickoffs (design §16 Phase 6)
- [ ] Light pen test on the BFF: JWT spoofing, refresh-token replay, prompt-injection through the refinement note, SQL injection on the match-id route
- [ ] Liability insurance (small E&O / commercial-general) — quote before payments go live
- [ ] Stripe Climate or similar optional contribution flag — nice to have, not blocking
- [ ] Email inbox provisioning (`privacy@`, `billing@`, `legal@`, `hello@`) tied to the operating-entity domain

---

## Out of scope for v1 (intentional)

- Habit loop / post-match scoring (design §9 / §16 Phase 6 — data captured, UI deferred)
- Multi-currency pricing (USD only at launch)
- Email notifications (defer to post-launch)
- Mobile native apps (web-first only)
