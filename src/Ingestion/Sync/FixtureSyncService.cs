using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WcPredictions.Data;
using WcPredictions.Ingestion.ApiFootball;

namespace WcPredictions.Ingestion.Sync;

// Pulls configured leagues' teams + fixtures from API-Football and upserts them
// into Postgres idempotently (keyed by provider ids). Safe to run repeatedly.
public sealed class FixtureSyncService(
    ApiFootballClient api,
    WcDbContext db,
    IOptions<ApiFootballOptions> options,
    ILogger<FixtureSyncService> log)
{
    public async Task SyncAsync(CancellationToken ct)
    {
        // Load once: cross-league overlap (e.g. Man City in EPL + UCL) means we
        // must reuse Team entities across iterations to avoid duplicate
        // ProviderTeamId inserts on a single SaveChanges.
        var byProvider = await db.Teams
            .Where(t => t.ProviderTeamId != null)
            .ToDictionaryAsync(t => t.ProviderTeamId!, ct);
        // Include Result so the final-score write path below can skip matches
        // that already have a row — otherwise EF would try to insert a duplicate
        // MatchResult (PK = MatchId) on the next sync.
        var existing = await db.Matches
            .Include(m => m.Result)
            .Where(m => m.ProviderFixtureId != null)
            .ToDictionaryAsync(m => m.ProviderFixtureId!, ct);

        foreach (var ls in options.Value.Leagues)
        {
            var info = await api.GetLeagueAsync(ls.LeagueId, ls.Season, ct);
            if (info is null) { log.LogWarning("League {Id} not returned", ls.LeagueId); continue; }

            var pLeague = ls.LeagueId.ToString();
            var league = await db.Leagues.FirstOrDefaultAsync(x => x.ProviderLeagueId == pLeague, ct)
                         ?? db.Leagues.Add(new League { Id = Guid.NewGuid(), ProviderLeagueId = pLeague }).Entity;
            league.Name = info.Name;

            var teams = await api.GetTeamsAsync(ls.LeagueId, ls.Season, ct);
            league.CompetitionType =
                teams.Count > 0 && teams.Count(t => t.National) * 2 >= teams.Count ? "national" : "club";

            foreach (var t in teams)
            {
                var key = t.Id.ToString();
                if (!byProvider.TryGetValue(key, out var team))
                {
                    team = new Team { Id = Guid.NewGuid(), ProviderTeamId = key };
                    db.Teams.Add(team);
                    byProvider[key] = team;
                }
                team.Name = t.Name;
                team.IsNational = t.National;
            }

            var fixtures = await api.GetFixturesAsync(ls.LeagueId, ls.Season, ct);

            // For group-format tournaments (e.g. WC), API-Football's fixture
            // round is "Group Stage - 1/2/3" — the matchweek, not the group
            // letter. The /standings endpoint carries the group label per team,
            // so we fetch it once per league and rewrite Stage to
            // "Group A · Stage 1" when a fixture sits in the group phase.
            var groupByTeam = await api.GetTeamGroupsAsync(ls.LeagueId, ls.Season, ct);

            foreach (var f in fixtures)
            {
                if (!byProvider.TryGetValue(f.HomeTeamId.ToString(), out var home) ||
                    !byProvider.TryGetValue(f.AwayTeamId.ToString(), out var away))
                {
                    log.LogWarning("Fixture {Id} references unknown team; skipped", f.Id);
                    continue;
                }

                var key = f.Id.ToString();
                if (!existing.TryGetValue(key, out var match))
                {
                    match = new Match { Id = Guid.NewGuid(), ProviderFixtureId = key };
                    db.Matches.Add(match);
                    existing[key] = match;
                }
                match.League = league;
                match.HomeTeam = home;
                match.AwayTeam = away;
                match.KickoffUtc = f.KickoffUtc;
                match.Status = f.Status;
                match.Stage = ComposeStage(f.Stage, f.HomeTeamId, groupByTeam);

                // Final-score capture. Once the provider reports the match
                // finished (FT regulation, AET extra time, PEN shoot-out)
                // and ships both goal totals, lock the result in. The row is
                // immutable — never overwrite once written — which is why we
                // only insert when Result is still null.
                if (match.Result is null
                    && IsFinished(f.Status)
                    && f.HomeGoals is int hg
                    && f.AwayGoals is int ag)
                {
                    match.Result = new MatchResult
                    {
                        MatchId = match.Id,
                        HomeGoals = hg,
                        AwayGoals = ag,
                        Outcome = hg > ag ? "H" : ag > hg ? "A" : "D",
                        SettledAt = DateTimeOffset.UtcNow,
                    };
                }
            }

            log.LogInformation(
                "Synced league {League}: {Teams} teams, {Fixtures} fixtures",
                info.Name, teams.Count, fixtures.Count);
        }

        await db.SaveChangesAsync(ct);
    }

    // API-Football status codes considered "match has a final score":
    // FT = full-time (regulation), AET = decided in extra time, PEN = decided on
    // penalties. We score the goal totals reported by the provider (which
    // include AET goals but exclude shoot-out kicks), matching what the
    // baseline prediction targets.
    private static bool IsFinished(string status) =>
        status is "FT" or "AET" or "PEN";

    // "Group Stage - 1" + team in Group A → "Group A · Stage 1". For non-group
    // rounds (knockouts, league phase, regular season), or when no group is
    // known, the original round label is preserved unchanged.
    private static string? ComposeStage(string? rawStage, int homeTeamId, IReadOnlyDictionary<int, string> groupByTeam)
    {
        if (string.IsNullOrWhiteSpace(rawStage)) return rawStage;
        if (!rawStage.StartsWith("Group Stage", StringComparison.OrdinalIgnoreCase)) return rawStage;
        if (!groupByTeam.TryGetValue(homeTeamId, out var group)) return rawStage;

        // "Group Stage - 1" → "Stage 1"; "Group Stage" → ""
        var dash = rawStage.IndexOf('-');
        var matchweek = dash > 0 ? rawStage[(dash + 1)..].Trim() : "";
        return string.IsNullOrEmpty(matchweek) ? group : $"{group} · Stage {matchweek}";
    }
}
