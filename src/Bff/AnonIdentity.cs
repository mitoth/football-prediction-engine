namespace WcPredictions.Bff;

// Anonymous identity for chat mode. Resolves the `mf_anon_id` HTTP-only cookie
// + the client IP off the request. If the cookie is missing, generates a fresh
// Guid and writes it as a Set-Cookie on the way back. The pair (AnonId, Ip) is
// the quota key for unsigned-in users.
public sealed record AnonIdentity(Guid AnonId, string Ip);

public static class AnonIdentityResolver
{
    public const string CookieName = "mf_anon_id";

    public static AnonIdentity Resolve(HttpContext ctx)
    {
        Guid anonId;
        if (!ctx.Request.Cookies.TryGetValue(CookieName, out var raw)
            || !Guid.TryParse(raw, out anonId))
        {
            anonId = Guid.NewGuid();
            // 30 days. HttpOnly so JS can't read it, Secure so it only flows on
            // HTTPS, SameSite=Lax so the cookie still rides on top-level navs
            // from the SPA on a different subdomain (wcaipredictions.com →
            // api.wcaipredictions.com).
            ctx.Response.Cookies.Append(CookieName, anonId.ToString(), new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromDays(30),
                Path = "/",
            });
        }

        // Container Apps puts the public client IP in X-Forwarded-For (first hop).
        // Fall back to the socket remote address in dev / local.
        var ip = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                 ?? ctx.Connection.RemoteIpAddress?.ToString()
                 ?? "unknown";
        return new AnonIdentity(anonId, ip);
    }
}
