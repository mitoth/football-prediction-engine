using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using WcPredictions.Data;

namespace WcPredictions.Bff;

// Resolves the Clerk identity on an authenticated request and lazily provisions
// the matching AppUser row (Clerk owns the user store; we keep a local mirror
// for FKs / quota). Tier travels in the session JWT (Clerk publicMetadata →
// `tier` claim, kept in sync by the Stripe webhook in Phase 5); we never do a
// second lookup. Free is the safe default.
public sealed class CurrentUser(ClaimsPrincipal principal, WcDbContext db)
{
    public string? ClerkUserId =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? principal.FindFirstValue("sub");

    public bool IsAuthenticated => !string.IsNullOrEmpty(ClerkUserId);

    // free | matchday | world_cup_tournament  (anything unknown ⇒ free).
    public string Tier
    {
        get
        {
            var t = principal.FindFirstValue("tier")?.Trim().ToLowerInvariant();
            return t is "matchday" or "world_cup_tournament" ? t : "free";
        }
    }

    private AppUser? _cached;

    public async Task<AppUser> ResolveAsync(CancellationToken ct)
    {
        if (_cached is not null) return _cached;
        var clerkId = ClerkUserId ?? throw new InvalidOperationException("Not authenticated");

        var user = await db.Users.SingleOrDefaultAsync(u => u.ClerkUserId == clerkId, ct);
        if (user is null)
        {
            user = new AppUser
            {
                Id = Guid.NewGuid(),
                ClerkUserId = clerkId,
                Timezone = principal.FindFirstValue("tz"),
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
        }
        return _cached = user;
    }
}
