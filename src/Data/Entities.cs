namespace WcPredictions.Data;

// §15 data model. Guid PKs unless noted. Timestamps are timestamptz
// (DateTimeOffset). JSON columns are Postgres jsonb (stored as string in v1).

// --- Identity / prefs -------------------------------------------------------

public class AppUser
{
    public Guid Id { get; set; }
    public string ClerkUserId { get; set; } = null!;
    public string? Timezone { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset? ExportRequestedAt { get; set; }

    public ICollection<UserLeague> Leagues { get; set; } = new List<UserLeague>();
    public ICollection<Refinement> Refinements { get; set; } = new List<Refinement>();
    public ICollection<Entitlement> Entitlements { get; set; } = new List<Entitlement>();
    public ICollection<QuotaLedger> QuotaLedger { get; set; } = new List<QuotaLedger>();
}

public class UserLeague
{
    public Guid UserId { get; set; }
    public Guid LeagueId { get; set; }
    public AppUser User { get; set; } = null!;
    public League League { get; set; } = null!;
}

// --- Football reference data ------------------------------------------------

public class League
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string CompetitionType { get; set; } = null!; // club | national
    public string? ProviderLeagueId { get; set; }

    public ICollection<Match> Matches { get; set; } = new List<Match>();
}

public class Team
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public bool IsNational { get; set; }
    public string? ProviderTeamId { get; set; }
}

public class Match
{
    public Guid Id { get; set; }
    public Guid LeagueId { get; set; }
    public Guid HomeTeamId { get; set; }
    public Guid AwayTeamId { get; set; }
    public DateTimeOffset KickoffUtc { get; set; }
    public string Status { get; set; } = null!;
    public string? ProviderFixtureId { get; set; }
    // Competition stage / round as reported by the provider (e.g. "Group Stage - 1",
    // "Round of 16", "Quarter-finals", "Regular Season - 12"). Used to sub-group the
    // match list under a league within a kickoff day.
    public string? Stage { get; set; }

    public League League { get; set; } = null!;
    public Team HomeTeam { get; set; } = null!;
    public Team AwayTeam { get; set; } = null!;
    public MatchResult? Result { get; set; }
    public ICollection<Baseline> Baselines { get; set; } = new List<Baseline>();
}

public class MatchResult
{
    public Guid MatchId { get; set; } // PK + FK 1:1
    public int HomeGoals { get; set; }
    public int AwayGoals { get; set; }
    public string Outcome { get; set; } = null!; // H | D | A
    public DateTimeOffset SettledAt { get; set; }

    public Match Match { get; set; } = null!;
}

// --- News -------------------------------------------------------------------

public class Article
{
    public Guid Id { get; set; }
    public string Headline { get; set; } = null!;
    public string Outlet { get; set; } = null!;
    public string Url { get; set; } = null!;
    public string Snippet { get; set; } = null!;
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset FetchedAt { get; set; }
}

// --- Predictions ------------------------------------------------------------

public class Baseline
{
    public Guid Id { get; set; }
    public Guid MatchId { get; set; }
    public int Version { get; set; }
    public string OutcomeProbs { get; set; } = null!; // jsonb {H,D,A}
    public int PredHome { get; set; }
    public int PredAway { get; set; }
    public double Confidence { get; set; }
    public string WhyText { get; set; } = null!;
    public string RefreshTrigger { get; set; } = null!; // t24h|lineup|breaking|stats-only
    public DateTimeOffset CreatedAt { get; set; }

    public Match Match { get; set; } = null!;
    public ICollection<BaselineCitation> Citations { get; set; } = new List<BaselineCitation>();
}

public class BaselineCitation
{
    public Guid BaselineId { get; set; }
    public Guid ArticleId { get; set; }
    public Baseline Baseline { get; set; } = null!;
    public Article Article { get; set; } = null!;
}

public class Refinement
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid MatchId { get; set; }
    public Guid BaselineVersionId { get; set; } // FK -> Baseline.Id forked from
    public string InputType { get; set; } = null!; // text | url
    public string? InputText { get; set; }
    public string? InputUrl { get; set; }
    public string? ExtractedText { get; set; }
    public string Status { get; set; } = null!; // rejected_gibberish|dead_url|off_topic|success|removed
    public bool QuotaCharged { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Refined output, persisted so the refined card + chip survive across
    // sessions without re-spending tokens. Set only when Status = success.
    public string? RefinedProbs { get; set; }      // jsonb {home,draw,away}
    public int? RefinedPredHome { get; set; }
    public int? RefinedPredAway { get; set; }
    public string? RefinedWhy { get; set; }
    public string? RefinedCitations { get; set; }  // jsonb string[] of article ids

    public AppUser User { get; set; } = null!;
    public Match Match { get; set; } = null!;
    public Baseline BaselineVersion { get; set; } = null!;
}

public class PredictionSnapshot
{
    public Guid Id { get; set; }
    public Guid MatchId { get; set; }
    public string SourceKind { get; set; } = null!; // baseline | refinement
    public Guid? BaselineId { get; set; }   // exactly one of these set (check constraint)
    public Guid? RefinementId { get; set; }
    public string OutcomeProbs { get; set; } = null!; // jsonb
    public int PredHome { get; set; }
    public int PredAway { get; set; }
    public DateTimeOffset CapturedAt { get; set; }

    public Match Match { get; set; } = null!;
    public Baseline? Baseline { get; set; }
    public Refinement? Refinement { get; set; }
    public SnapshotScore? Score { get; set; }
}

public class SnapshotScore
{
    public Guid SnapshotId { get; set; } // PK + FK 1:1
    public Guid MatchResultId { get; set; } // FK -> MatchResult.MatchId
    public double Brier { get; set; }
    public double ScorelineDistance { get; set; }
    public bool BeatBaseline { get; set; }

    public PredictionSnapshot Snapshot { get; set; } = null!;
    public MatchResult MatchResult { get; set; } = null!;
}

// --- Money / entitlement ----------------------------------------------------

public class Entitlement
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string PassType { get; set; } = null!; // matchday | world_cup_tournament
    public DateOnly? ScopeMatchDay { get; set; }
    public Guid? ScopeTournamentId { get; set; } // -> League.Id
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset ValidTo { get; set; }
    public string? StripeCheckoutId { get; set; }
    public string Status { get; set; } = null!; // active | refunded

    public AppUser User { get; set; } = null!;
}

public class QuotaLedger
{
    public Guid UserId { get; set; }
    public DateOnly QuotaDate { get; set; }
    public int SuccessCount { get; set; }

    public AppUser User { get; set; } = null!;
}
