using Microsoft.EntityFrameworkCore;
using WcPredictions.Data;

namespace WcPredictions.Bff;

// Chat mode quota.
//   Anonymous (no login) → 3 messages/day, keyed by (mf_anon_id cookie, IP).
//   Signed-in free       → 5 messages/day, keyed by UserId.
//   Pass holders         → 30/day fair-use ceiling (Phase 5).
// Only successful+relevant turns consume a credit; gibberish / off-topic don't.
//
// QuotaLedger is the durable authority for signed-in users; AnonQuotaLedger for
// anonymous. Both reset implicitly at the day boundary (new row per date).
//
// PHASE-5 STRIPE TODO: the 30/day cap must be disclosed on the payment page
// *before* the user clicks Pay. Worst-case unchecked abuse over a 35-day WC
// tournament is roughly $73 in Claude API cost on a $5 pass — see
// docs/launch-prep-checklist.md → "Stripe (Phase 5)" → "MANDATORY ON PAYMENT
// PAGE" for the exact disclosure copy and the link to ToS §4.
public sealed class QuotaService(WcDbContext db)
{
    // Tier values: "free" | "matchday" | "world_cup_tournament". Anything that
    // is not "free" gets the paid fair-use ceiling.
    public const int AnonDailyCap = 3;
    public const int FreeDailyCap = 5;
    public const int PaidDailyCap = 30;
    public static int Cap(string tier) => tier == "free" ? FreeDailyCap : PaidDailyCap;

    private static DateOnly Today(string? tz)
    {
        var now = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(tz))
        {
            try { now = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(tz)); }
            catch { /* unknown tz id → UTC */ }
        }
        return DateOnly.FromDateTime(now.DateTime);
    }

    public async Task<int> RemainingAsync(AppUser user, string tier, CancellationToken ct)
    {
        var used = await db.QuotaLedger
            .Where(q => q.UserId == user.Id && q.QuotaDate == Today(user.Timezone))
            .Select(q => (int?)q.SuccessCount).FirstOrDefaultAsync(ct) ?? 0;
        return Math.Max(0, Cap(tier) - used);
    }

    public async Task ConsumeAsync(AppUser user, CancellationToken ct)
    {
        var date = Today(user.Timezone);
        var row = await db.QuotaLedger
            .SingleOrDefaultAsync(q => q.UserId == user.Id && q.QuotaDate == date, ct);
        if (row is null)
            db.QuotaLedger.Add(new QuotaLedger { UserId = user.Id, QuotaDate = date, SuccessCount = 1 });
        else
            row.SuccessCount++;
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> RemainingAnonAsync(Guid anonId, string ip, CancellationToken ct)
    {
        var date = Today(null);
        var used = await db.AnonQuotaLedger
            .Where(q => q.AnonId == anonId && q.Ip == ip && q.QuotaDate == date)
            .Select(q => (int?)q.SuccessCount).FirstOrDefaultAsync(ct) ?? 0;
        return Math.Max(0, AnonDailyCap - used);
    }

    public async Task ConsumeAnonAsync(Guid anonId, string ip, CancellationToken ct)
    {
        var date = Today(null);
        var row = await db.AnonQuotaLedger
            .SingleOrDefaultAsync(q => q.AnonId == anonId && q.Ip == ip && q.QuotaDate == date, ct);
        if (row is null)
            db.AnonQuotaLedger.Add(new AnonQuotaLedger { AnonId = anonId, Ip = ip, QuotaDate = date, SuccessCount = 1 });
        else
            row.SuccessCount++;
        await db.SaveChangesAsync(ct);
    }
}
