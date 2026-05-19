using Microsoft.EntityFrameworkCore;
using WcPredictions.Data;

namespace WcPredictions.Bff;

// Free = 3 successful refinements/day; pass holders unlimited but with a
// ~30/day fair-use ceiling (passes arrive in Phase 5 — the cap is wired now).
// Only successful+relevant refinements consume a credit; gibberish / off-topic
// / dead URL never do, so the counter never feels adversarial.
//
// QuotaLedger is the durable authority (resets implicitly: a new day = a new
// (UserId, QuotaDate) row). Reset is midnight in the user's account timezone,
// UTC fallback — matches the design.
public sealed class QuotaService(WcDbContext db)
{
    public static int Cap(string tier) => tier == "free" ? 3 : 30;

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
}
