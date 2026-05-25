using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WcPredictions.Data;

namespace WcPredictions.Bff;

// The product hook. All authed. The BFF owns the business rules: verify the
// Clerk JWT + tier, enforce the daily quota, call the engine for the
// prediction, persist the Refinement + snapshot. Only a successful+relevant
// refinement spends a credit; gibberish / off-topic never do. One active chip
// per match per user; editing or removing it is free.
//
// URL refinements are disabled: fetching arbitrary publisher content for
// commercial reuse creates legal exposure. Only free-text notes are accepted.

public sealed record RefineInput(string InputType, string? Text, string? Url); // text only

public sealed record ChipView(string InputType, string? Text, string? Url, string Status);

public sealed record RefinedView(
    double Home, double Draw, double Away,
    int PredHome, int PredAway, string Why, IReadOnlyList<CitationView> Citations);

public sealed record RefineResponse(
    string Status, bool Applied, int QuotaRemaining,
    ChipView? Chip, RefinedView? Refined);

public sealed record MeView(string Tier, int QuotaRemaining, ChipView? Chip, RefinedView? Refined);

public static class RefineEndpoints
{
    public static void MapRefineEndpoints(this WebApplication app)
    {
        // Authed per-user state for a match: tier, remaining quota, active chip,
        // and the persisted refined card (survives across sessions).
        app.MapGet("/matches/{id:guid}/me", async (
            Guid id, CurrentUser me, QuotaService quota, WcDbContext db, CancellationToken ct) =>
        {
            var user = await me.ResolveAsync(ct);
            var remaining = await quota.RemainingAsync(user, me.Tier, ct);
            var (chip, refined) = await ActiveAsync(db, user.Id, id, ct);
            return Results.Ok(new MeView(me.Tier, remaining, chip, refined));
        }).RequireAuthorization();

        // New chip → costs one credit (only if it lands as success).
        app.MapPost("/matches/{id:guid}/refine", async (
            Guid id, RefineInput input, CurrentUser me, QuotaService quota,
            PredictionEngineClient engine, WcDbContext db, CancellationToken ct) =>
        {
            if (IsUrl(input))
                return Results.BadRequest(new { error = "url_disabled",
                    message = "URL refinements are disabled. Send a free-text note instead." });

            var user = await me.ResolveAsync(ct);
            var remaining = await quota.RemainingAsync(user, me.Tier, ct);
            if (remaining <= 0)
                return Results.Json(new RefineResponse("quota_exhausted", false, 0,
                    (await ActiveAsync(db, user.Id, id, ct)).chip, null), statusCode: 429);

            return await RunAsync(id, input, user, charge: true, remaining,
                me, quota, engine, db, ct);
        }).RequireAuthorization();

        // Edit the active chip → free re-run on the same credit.
        app.MapPut("/matches/{id:guid}/refine", async (
            Guid id, RefineInput input, CurrentUser me, QuotaService quota,
            PredictionEngineClient engine, WcDbContext db, CancellationToken ct) =>
        {
            if (IsUrl(input))
                return Results.BadRequest(new { error = "url_disabled",
                    message = "URL refinements are disabled. Send a free-text note instead." });

            var user = await me.ResolveAsync(ct);
            var latest = await LatestAsync(db, user.Id, id, ct);
            if (latest is null || latest.Status == "removed")
                return Results.Json(new RefineResponse("no_active_chip", false,
                    await quota.RemainingAsync(user, me.Tier, ct), null, null), statusCode: 409);

            var remaining = await quota.RemainingAsync(user, me.Tier, ct);
            return await RunAsync(id, input, user, charge: false, remaining,
                me, quota, engine, db, ct);
        }).RequireAuthorization();

        // Remove the chip → revert to the baseline (tombstone row, free).
        app.MapDelete("/matches/{id:guid}/refine", async (
            Guid id, CurrentUser me, QuotaService quota, WcDbContext db, CancellationToken ct) =>
        {
            var user = await me.ResolveAsync(ct);
            var latest = await LatestAsync(db, user.Id, id, ct);
            if (latest is not null && latest.Status != "removed")
            {
                db.Refinements.Add(new Refinement
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    MatchId = id,
                    BaselineVersionId = latest.BaselineVersionId,
                    InputType = "text",
                    Status = "removed",
                    QuotaCharged = false,
                    CreatedAt = DateTimeOffset.UtcNow,
                });
                await db.SaveChangesAsync(ct);
            }
            return Results.Ok(new RefineResponse("removed", false,
                await quota.RemainingAsync(user, me.Tier, ct), null, null));
        }).RequireAuthorization();
    }

    private static bool IsUrl(RefineInput input) =>
        string.Equals(input.InputType, "url", StringComparison.OrdinalIgnoreCase)
        || !string.IsNullOrWhiteSpace(input.Url);

    private static async Task<IResult> RunAsync(
        Guid matchId, RefineInput input, AppUser user, bool charge, int remaining,
        CurrentUser me, QuotaService quota, PredictionEngineClient engine,
        WcDbContext db, CancellationToken ct)
    {
        var baseline = await db.Baselines
            .Where(b => b.MatchId == matchId)
            .OrderByDescending(b => b.Version)
            .FirstOrDefaultAsync(ct);
        if (baseline is null)
            return Results.Json(new RefineResponse("no_baseline", false, remaining, null, null),
                statusCode: 409);

        var note = input.Text ?? "";
        var result = await engine.RefineAsync(matchId, baseline.Id, note, ct);

        if (result.Status != "success")
        {
            await SaveRefinement(db, user, matchId, baseline.Id, input,
                extracted: null, result.Status, charged: false, refined: null, ct);
            return Results.Ok(new RefineResponse(result.Status, false, remaining,
                new ChipView(input.InputType, input.Text, input.Url, result.Status), null));
        }

        await SaveRefinement(db, user, matchId, baseline.Id, input,
            extracted: null, "success", charged: charge, refined: result, ct);
        if (charge) await quota.ConsumeAsync(user, ct);

        var refined = await ToRefinedView(db,
            result.Home, result.Draw, result.Away, result.PredHome, result.PredAway,
            result.Why, result.Citations, ct);
        return Results.Ok(new RefineResponse("success", true,
            charge ? remaining - 1 : remaining,
            new ChipView(input.InputType, input.Text, input.Url, "success"), refined));
    }

    private static async Task SaveRefinement(
        WcDbContext db, AppUser user, Guid matchId, Guid baselineId, RefineInput input,
        string? extracted, string status, bool charged, RefineResult? refined,
        CancellationToken ct)
    {
        var r = new Refinement
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            MatchId = matchId,
            BaselineVersionId = baselineId,
            InputType = input.InputType,
            InputText = input.Text,
            InputUrl = input.Url,
            ExtractedText = extracted,
            Status = status,
            QuotaCharged = charged,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        if (refined is not null)
        {
            r.RefinedProbs = JsonSerializer.Serialize(new
            {
                home = refined.Home, draw = refined.Draw, away = refined.Away,
            });
            r.RefinedPredHome = refined.PredHome;
            r.RefinedPredAway = refined.PredAway;
            r.RefinedWhy = refined.Why;
            r.RefinedCitations = JsonSerializer.Serialize(refined.Citations);
        }
        db.Refinements.Add(r);

        if (status == "success")
            db.PredictionSnapshots.Add(new PredictionSnapshot
            {
                Id = Guid.NewGuid(),
                MatchId = matchId,
                SourceKind = "refinement",
                RefinementId = r.Id,
                OutcomeProbs = r.RefinedProbs!,
                PredHome = refined!.PredHome,
                PredAway = refined.PredAway,
                CapturedAt = DateTimeOffset.UtcNow,
            });

        await db.SaveChangesAsync(ct);
    }

    private static Task<Refinement?> LatestAsync(
        WcDbContext db, Guid userId, Guid matchId, CancellationToken ct) =>
        db.Refinements
            .Where(r => r.UserId == userId && r.MatchId == matchId)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(ct);

    private static async Task<(ChipView? chip, RefinedView? refined)> ActiveAsync(
        WcDbContext db, Guid userId, Guid matchId, CancellationToken ct)
    {
        var latest = await LatestAsync(db, userId, matchId, ct);
        if (latest is null || latest.Status == "removed") return (null, null);

        var chip = new ChipView(latest.InputType, latest.InputText, latest.InputUrl, latest.Status);
        if (latest.Status != "success" || latest.RefinedProbs is null) return (chip, null);

        using var p = JsonDocument.Parse(latest.RefinedProbs);
        var ids = JsonSerializer.Deserialize<List<string>>(latest.RefinedCitations ?? "[]") ?? [];
        var refined = await ToRefinedView(db,
            p.RootElement.GetProperty("home").GetDouble(),
            p.RootElement.GetProperty("draw").GetDouble(),
            p.RootElement.GetProperty("away").GetDouble(),
            latest.RefinedPredHome ?? 0, latest.RefinedPredAway ?? 0,
            latest.RefinedWhy ?? "", ids, ct);
        return (chip, refined);
    }

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
