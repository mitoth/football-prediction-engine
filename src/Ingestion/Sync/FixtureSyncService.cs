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
        var existing = await db.Matches
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
            }

            log.LogInformation(
                "Synced league {League}: {Teams} teams, {Fixtures} fixtures",
                info.Name, teams.Count, fixtures.Count);
        }

        await db.SaveChangesAsync(ct);
    }
}
