using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WcPredictions.Data;

namespace WcPredictions.Bff;

// Chat-mode refinement. One POST per user turn; the frontend sends the full
// thread each call (session-only — backend doesn't load history). Authentication
// is OPTIONAL: signed-in users charge against their 5/day signed quota,
// anonymous users charge against a 3/day anon quota keyed by (cookie + IP).
// The same Refinement table backs both — only the identity columns differ.

public sealed record ChatMessage(string Role, string Text);
public sealed record ChatRequest(IReadOnlyList<ChatMessage> Messages);

public sealed record ChatResponse(
    string Status,                 // success | rejected_gibberish | off_topic | quota_exhausted | no_baseline | invalid_thread
    bool Applied,
    int QuotaRemaining,
    string Tier,                   // anon | free | matchday | world_cup_tournament
    RefinedView? Refined,
    string? Message);              // human-friendly reason on non-success

public static class ChatEndpoints
{
    private const int MaxMessages = 20;
    // Per-message char ceiling depends on identity tier. Anon stays tight so a
    // guest can't burn the prompt budget on a single turn before signing in;
    // signed-in tiers get more headroom for nuanced tactical notes. Assistant
    // turns echo Claude's `why` paragraph (engine-controlled, not user input)
    // so no client cap — we trust the LLM-side response shape.
    public const int AnonUserMessageMax = 150;
    public const int SignedUserMessageMax = 750;

    public static void MapChatEndpoints(this WebApplication app)
    {
        app.MapPost("/matches/{id:guid}/chat", async (
            Guid id, ChatRequest body, HttpContext ctx,
            CurrentUser me, QuotaService quota,
            PredictionEngineClient engine, WcDbContext db, CancellationToken ct) =>
        {
            // ---- Identity + quota (resolved first; the user-message char cap
            // depends on tier) ---------------------------------------------------
            string tier;
            AppUser? signedInUser = null;
            AnonIdentity? anon = null;
            int remaining;

            if (me.IsAuthenticated)
            {
                signedInUser = await me.ResolveAsync(ct);
                tier = me.Tier;
                remaining = await quota.RemainingAsync(signedInUser, tier, ct);
            }
            else
            {
                anon = AnonIdentityResolver.Resolve(ctx);
                tier = "anon";
                remaining = await quota.RemainingAnonAsync(anon.AnonId, anon.Ip, ct);
            }

            var userMessageCap = tier == "anon" ? AnonUserMessageMax : SignedUserMessageMax;

            // ---- Validate body ------------------------------------------------
            var msgs = body.Messages;
            if (msgs is null || msgs.Count == 0 || msgs.Count > MaxMessages)
                return Bad("invalid_thread", $"Messages must be 1..{MaxMessages}.");
            if (!string.Equals(msgs[^1].Role, "user", StringComparison.OrdinalIgnoreCase))
                return Bad("invalid_thread", "The last message must come from the user.");
            foreach (var m in msgs)
            {
                if (string.IsNullOrWhiteSpace(m.Text))
                    return Bad("invalid_message", "Each message must be non-empty.");
                if (m.Role is not ("user" or "assistant"))
                    return Bad("invalid_message", "Role must be 'user' or 'assistant'.");
                // Only validate USER message length — assistant turns echo the
                // model's own `why` and are not user input.
                if (m.Role == "user" && m.Text.Length > userMessageCap)
                    return Bad("invalid_message", $"Each user message must be 1..{userMessageCap} chars.");
            }

            if (remaining <= 0)
            {
                var message = tier == "anon"
                    ? "You've used all 3 free messages today. Sign in for 5 more."
                    : "You've used today's allowance. Resets at midnight.";
                return Results.Json(new ChatResponse(
                    "quota_exhausted", false, 0, tier, null, message), statusCode: 429);
            }

            // ---- Baseline -----------------------------------------------------
            var baseline = await db.Baselines
                .Where(b => b.MatchId == id)
                .OrderByDescending(b => b.Version)
                .FirstOrDefaultAsync(ct);
            if (baseline is null)
                return Results.Json(new ChatResponse(
                    "no_baseline", false, remaining, tier, null,
                    "Baseline prediction not ready yet."), statusCode: 409);

            // ---- Engine call --------------------------------------------------
            var latestNote = msgs[^1].Text;
            var engineMessages = msgs
                .Select(m => new EngineChatTurn(m.Role.ToLowerInvariant(), m.Text))
                .ToList();
            var result = await engine.RefineAsync(id, baseline.Id, latestNote, engineMessages, ct);

            // ---- Persist + consume quota -------------------------------------
            var refinement = new Refinement
            {
                Id = Guid.NewGuid(),
                UserId = signedInUser?.Id,
                AnonId = anon?.AnonId,
                Ip = anon?.Ip,
                MatchId = id,
                BaselineVersionId = baseline.Id,
                InputType = "text",
                InputText = latestNote,
                Status = result.Status,
                QuotaCharged = result.Status == "success",
                CreatedAt = DateTimeOffset.UtcNow,
            };

            if (result.Status == "success")
            {
                refinement.RefinedProbs = JsonSerializer.Serialize(new
                {
                    home = result.Home, draw = result.Draw, away = result.Away,
                });
                refinement.RefinedPredHome = result.PredHome;
                refinement.RefinedPredAway = result.PredAway;
                refinement.RefinedWhy = result.Why;
                refinement.RefinedCitations = JsonSerializer.Serialize(result.Citations);
            }

            db.Refinements.Add(refinement);

            if (result.Status == "success")
            {
                db.PredictionSnapshots.Add(new PredictionSnapshot
                {
                    Id = Guid.NewGuid(),
                    MatchId = id,
                    SourceKind = "refinement",
                    RefinementId = refinement.Id,
                    OutcomeProbs = refinement.RefinedProbs!,
                    PredHome = result.PredHome,
                    PredAway = result.PredAway,
                    CapturedAt = DateTimeOffset.UtcNow,
                });
            }

            await db.SaveChangesAsync(ct);

            if (result.Status == "success")
            {
                if (signedInUser is not null) await quota.ConsumeAsync(signedInUser, ct);
                else await quota.ConsumeAnonAsync(anon!.AnonId, anon.Ip, ct);
                remaining -= 1;
            }

            var refined = result.Status == "success"
                ? await ToRefinedView(db,
                    result.Home, result.Draw, result.Away,
                    result.PredHome, result.PredAway, result.Why, result.Citations, ct)
                : null;

            var statusMessage = result.Status switch
            {
                "rejected_gibberish" => "That didn't read like a sentence — try a real note about the match.",
                "off_topic" => "That doesn't seem to be about this match. Try something about the teams, tactics, or conditions.",
                _ => null,
            };

            return Results.Ok(new ChatResponse(
                result.Status, result.Status == "success", remaining, tier, refined, statusMessage));
        }).RequireRateLimiting("refine-per-ip");

        // Lightweight read of the caller's quota state + their per-message
        // character cap — used by the SPA to hydrate the chat panel before the
        // first send (anon or signed-in).
        app.MapGet("/matches/{id:guid}/chat-status", async (
            HttpContext ctx, CurrentUser me, QuotaService quota, CancellationToken ct) =>
        {
            if (me.IsAuthenticated)
            {
                var user = await me.ResolveAsync(ct);
                var remaining = await quota.RemainingAsync(user, me.Tier, ct);
                return Results.Ok(new
                {
                    tier = me.Tier,
                    quotaRemaining = remaining,
                    userMessageMax = SignedUserMessageMax,
                });
            }

            var anon = AnonIdentityResolver.Resolve(ctx);
            var remainingAnon = await quota.RemainingAnonAsync(anon.AnonId, anon.Ip, ct);
            return Results.Ok(new
            {
                tier = "anon",
                quotaRemaining = remainingAnon,
                userMessageMax = AnonUserMessageMax,
            });
        });
    }

    private static IResult Bad(string code, string message) =>
        Results.BadRequest(new { error = code, message });

    private static async Task<RefinedView> ToRefinedView(
        WcDbContext db, double h, double d, double a, int ph, int pa,
        string why, IReadOnlyList<string> citationIds, CancellationToken ct)
    {
        var ids = citationIds.Select(x => Guid.TryParse(x, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty).ToList();
        var cites = await db.Articles
            .Where(x => ids.Contains(x.Id))
            .Select(x => new CitationView(x.Id, x.Headline, x.Outlet, x.Url, x.Snippet))
            .ToListAsync(ct);
        return new RefinedView(h, d, a, ph, pa, why, cites);
    }
}
