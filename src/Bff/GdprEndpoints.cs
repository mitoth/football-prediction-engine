using Microsoft.EntityFrameworkCore;
using WcPredictions.Data;

namespace WcPredictions.Bff;

// GDPR Articles 15 (right to access / export) + 17 (right to erasure / delete).
// Both endpoints authed via the same Clerk JWT pipeline as the rest of the
// authed surface. Export = JSON dump of every row tied to the user. Delete =
// hard-delete refinements + snapshots + quota + entitlements + the AppUser
// row. Stripe (and Clerk) keep their own immutable records on their side for
// tax and compliance — the user gets pointed at those in the response.
public static class GdprEndpoints
{
    public static void MapGdprEndpoints(this WebApplication app)
    {
        // GET /me/export — full JSON snapshot of every row tied to the user.
        app.MapGet("/me/export", async (
            CurrentUser me, WcDbContext db, CancellationToken ct) =>
        {
            var user = await me.ResolveAsync(ct);

            var refinements = await db.Refinements
                .Where(r => r.UserId == user.Id)
                .OrderBy(r => r.CreatedAt)
                .Select(r => new
                {
                    r.Id, r.MatchId, r.BaselineVersionId, r.InputType,
                    r.InputText, r.InputUrl, r.ExtractedText, r.Status,
                    r.QuotaCharged, r.RefinedProbs, r.RefinedPredHome,
                    r.RefinedPredAway, r.RefinedWhy, r.RefinedCitations,
                    r.CreatedAt,
                })
                .ToListAsync(ct);

            var refIds = refinements.Select(r => r.Id).ToList();
            var snapshots = await db.PredictionSnapshots
                .Where(s => s.RefinementId != null && refIds.Contains(s.RefinementId!.Value))
                .OrderBy(s => s.CapturedAt)
                .Select(s => new
                {
                    s.Id, s.MatchId, s.SourceKind, s.RefinementId,
                    s.OutcomeProbs, s.PredHome, s.PredAway, s.CapturedAt,
                })
                .ToListAsync(ct);

            var quota = await db.QuotaLedger
                .Where(q => q.UserId == user.Id)
                .OrderBy(q => q.QuotaDate)
                .Select(q => new { q.QuotaDate, q.SuccessCount })
                .ToListAsync(ct);

            var entitlements = await db.Entitlements
                .Where(e => e.UserId == user.Id)
                .OrderBy(e => e.ValidFrom)
                .Select(e => new
                {
                    e.Id, e.PassType, e.ValidFrom, e.ValidTo, e.Status,
                    e.StripeCheckoutId, e.ScopeMatchDay, e.ScopeTournamentId,
                })
                .ToListAsync(ct);

            var leagues = await db.UserLeagues
                .Where(l => l.UserId == user.Id)
                .Select(l => new { l.LeagueId })
                .ToListAsync(ct);

            var export = new
            {
                exportedAt = DateTimeOffset.UtcNow,
                user = new
                {
                    user.Id,
                    user.ClerkUserId,
                    user.Timezone,
                    user.CreatedAt,
                    user.DeletedAt,
                    user.ExportRequestedAt,
                },
                favouriteLeagues = leagues,
                refinements,
                predictionSnapshots = snapshots,
                quotaLedger = quota,
                entitlements,
                notes = new
                {
                    description = "GDPR Article 15 data export. JSON shape is unstable; treat field names as informational.",
                    notIncluded = new[]
                    {
                        "Email / password — held by Clerk; see clerk.com/legal/privacy for their export.",
                        "Payment card details — held by Stripe; see your Stripe receipt history.",
                        "Server-side logs and request traces beyond a 14-day rolling window.",
                    },
                },
            };

            // Stamp the export request so future audits can confirm we honoured it.
            user.ExportRequestedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);

            return Results.Json(export, statusCode: 200);
        }).RequireAuthorization();

        // DELETE /me — erase personal data tied to the user (Article 17).
        app.MapDelete("/me", async (
            CurrentUser me, WcDbContext db, CancellationToken ct) =>
        {
            var user = await me.ResolveAsync(ct);
            var userId = user.Id;

            // Refinement snapshots first (FK -> Refinement.Id).
            var refIds = await db.Refinements
                .Where(r => r.UserId == userId)
                .Select(r => r.Id)
                .ToListAsync(ct);

            await db.PredictionSnapshots
                .Where(s => s.RefinementId != null && refIds.Contains(s.RefinementId!.Value))
                .ExecuteDeleteAsync(ct);

            await db.Refinements
                .Where(r => r.UserId == userId)
                .ExecuteDeleteAsync(ct);

            await db.QuotaLedger
                .Where(q => q.UserId == userId)
                .ExecuteDeleteAsync(ct);

            // Entitlements: hard-delete. Stripe retains the immutable receipt /
            // tax record on their side, so deleting our copy does not leave a
            // gap in financial reporting. If the law in our home jurisdiction
            // turns out to require us to keep entitlement rows ourselves, this
            // becomes an anonymisation (set UserId nullable + null it out).
            await db.Entitlements
                .Where(e => e.UserId == userId)
                .ExecuteDeleteAsync(ct);

            await db.UserLeagues
                .Where(l => l.UserId == userId)
                .ExecuteDeleteAsync(ct);

            await db.Users
                .Where(u => u.Id == userId)
                .ExecuteDeleteAsync(ct);

            return Results.Ok(new
            {
                deleted = true,
                appUserId = userId,
                next = new
                {
                    clerk = "Sign in to your Clerk account at https://accounts.clerk.com and delete it there to remove the email and password as well.",
                    stripe = "If you have an active paid pass, contact billing@matchforecast.app for a pro-rata refund. Stripe retains anonymised receipts for tax purposes.",
                },
            });
        }).RequireAuthorization();
    }
}
