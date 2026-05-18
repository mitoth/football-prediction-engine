using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace WcPredictions.Ingestion.ApiFootball;

public sealed class ApiFootballOptions
{
    public const string Section = "ApiFootball";
    public string BaseUrl { get; set; } = "https://v3.football.api-sports.io";
    public string ApiKey { get; set; } = "";
    // League id + season pairs to ingest. Defaults: World Cup, Premier League,
    // Champions League. API-Football ids: WC=1, PL=39, CL=2.
    public List<LeagueSeason> Leagues { get; set; } =
    [
        new() { LeagueId = 1, Season = 2026 },   // FIFA World Cup 2026
        new() { LeagueId = 39, Season = 2025 },  // Premier League 2025/26
        new() { LeagueId = 2, Season = 2025 },   // Champions League 2025/26
    ];
}

public sealed class LeagueSeason
{
    public int LeagueId { get; set; }
    public int Season { get; set; }
}

// Minimal projections of the API-Football v3 response envelope. Only fields the
// ingestion pipeline maps are modelled.
public sealed record LeagueInfo(int Id, string Name, string Type, string Country);
public sealed record TeamInfo(int Id, string Name, bool National);
public sealed record FixtureInfo(
    int Id, DateTimeOffset KickoffUtc, string Status,
    int HomeTeamId, string HomeTeamName, int AwayTeamId, string AwayTeamName);

public sealed class ApiFootballClient(HttpClient http)
{
    public async Task<LeagueInfo?> GetLeagueAsync(int leagueId, int season, CancellationToken ct)
    {
        var env = await http.GetFromJsonAsync<Envelope<LeagueNode>>(
            $"/leagues?id={leagueId}&season={season}", ct);
        var n = env?.Response.FirstOrDefault();
        return n is null ? null
            : new LeagueInfo(n.League.Id, n.League.Name, n.League.Type, n.Country.Name);
    }

    public async Task<IReadOnlyList<TeamInfo>> GetTeamsAsync(int leagueId, int season, CancellationToken ct)
    {
        var env = await http.GetFromJsonAsync<Envelope<TeamNode>>(
            $"/teams?league={leagueId}&season={season}", ct);
        return env?.Response.Select(x => new TeamInfo(x.Team.Id, x.Team.Name, x.Team.National)).ToList()
            ?? [];
    }

    public async Task<IReadOnlyList<FixtureInfo>> GetFixturesAsync(int leagueId, int season, CancellationToken ct)
    {
        var env = await http.GetFromJsonAsync<Envelope<FixtureNode>>(
            $"/fixtures?league={leagueId}&season={season}", ct);
        return env?.Response.Select(x => new FixtureInfo(
            x.Fixture.Id, x.Fixture.Date, x.Fixture.Status.Short,
            x.Teams.Home.Id, x.Teams.Home.Name,
            x.Teams.Away.Id, x.Teams.Away.Name)).ToList() ?? [];
    }

    // ---- response envelope DTOs ----
    private sealed class Envelope<T> { [JsonPropertyName("response")] public List<T> Response { get; set; } = []; }
    private sealed class LeagueNode { public LeagueDto League { get; set; } = new(); public CountryDto Country { get; set; } = new(); }
    private sealed class LeagueDto { public int Id { get; set; } public string Name { get; set; } = ""; public string Type { get; set; } = ""; }
    private sealed class CountryDto { public string Name { get; set; } = ""; }
    private sealed class TeamNode { public TeamDto Team { get; set; } = new(); }
    private sealed class TeamDto { public int Id { get; set; } public string Name { get; set; } = ""; public bool National { get; set; } }
    private sealed class FixtureNode { public FixtureDto Fixture { get; set; } = new(); public TeamsDto Teams { get; set; } = new(); }
    private sealed class FixtureDto { public int Id { get; set; } public DateTimeOffset Date { get; set; } public StatusDto Status { get; set; } = new(); }
    private sealed class StatusDto { [JsonPropertyName("short")] public string Short { get; set; } = ""; }
    private sealed class TeamsDto { public SideDto Home { get; set; } = new(); public SideDto Away { get; set; } = new(); }
    private sealed class SideDto { public int Id { get; set; } public string Name { get; set; } = ""; }
}
