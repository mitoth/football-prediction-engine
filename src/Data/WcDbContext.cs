using Microsoft.EntityFrameworkCore;

namespace WcPredictions.Data;

public class WcDbContext(DbContextOptions<WcDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<UserLeague> UserLeagues => Set<UserLeague>();
    public DbSet<League> Leagues => Set<League>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchResult> MatchResults => Set<MatchResult>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<Baseline> Baselines => Set<Baseline>();
    public DbSet<BaselineCitation> BaselineCitations => Set<BaselineCitation>();
    public DbSet<Refinement> Refinements => Set<Refinement>();
    public DbSet<PredictionSnapshot> PredictionSnapshots => Set<PredictionSnapshot>();
    public DbSet<SnapshotScore> SnapshotScores => Set<SnapshotScore>();
    public DbSet<Entitlement> Entitlements => Set<Entitlement>();
    public DbSet<QuotaLedger> QuotaLedger => Set<QuotaLedger>();
    public DbSet<AnonQuotaLedger> AnonQuotaLedger => Set<AnonQuotaLedger>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppUser>(e =>
        {
            e.HasIndex(x => x.ClerkUserId).IsUnique();
        });

        // Provider-id unique indexes make ingestion upserts idempotent. Columns
        // are nullable; Postgres allows multiple NULLs in a unique index, so
        // manually-seeded rows without a provider id don't collide.
        b.Entity<League>(e => e.HasIndex(x => x.ProviderLeagueId).IsUnique());
        b.Entity<Team>(e => e.HasIndex(x => x.ProviderTeamId).IsUnique());
        b.Entity<Article>(e => e.HasIndex(x => x.Url).IsUnique());

        b.Entity<UserLeague>(e =>
        {
            e.HasKey(x => new { x.UserId, x.LeagueId });
            e.HasOne(x => x.User).WithMany(u => u.Leagues)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.League).WithMany()
                .HasForeignKey(x => x.LeagueId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Match>(e =>
        {
            e.HasOne(x => x.League).WithMany(l => l.Matches)
                .HasForeignKey(x => x.LeagueId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.HomeTeam).WithMany()
                .HasForeignKey(x => x.HomeTeamId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.AwayTeam).WithMany()
                .HasForeignKey(x => x.AwayTeamId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.ProviderFixtureId).IsUnique();
            e.HasIndex(x => x.KickoffUtc);
        });

        b.Entity<MatchResult>(e =>
        {
            e.HasKey(x => x.MatchId);
            e.HasOne(x => x.Match).WithOne(m => m.Result)
                .HasForeignKey<MatchResult>(x => x.MatchId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Baseline>(e =>
        {
            e.Property(x => x.OutcomeProbs).HasColumnType("jsonb");
            e.HasIndex(x => new { x.MatchId, x.Version }).IsUnique();
            e.HasOne(x => x.Match).WithMany(m => m.Baselines)
                .HasForeignKey(x => x.MatchId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<BaselineCitation>(e =>
        {
            e.HasKey(x => new { x.BaselineId, x.ArticleId });
            e.HasOne(x => x.Baseline).WithMany(bl => bl.Citations)
                .HasForeignKey(x => x.BaselineId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Article).WithMany()
                .HasForeignKey(x => x.ArticleId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Refinement>(e =>
        {
            e.Property(x => x.RefinedProbs).HasColumnType("jsonb");
            e.Property(x => x.RefinedCitations).HasColumnType("jsonb");
            e.HasOne(x => x.User).WithMany(u => u.Refinements)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Match).WithMany()
                .HasForeignKey(x => x.MatchId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.BaselineVersion).WithMany()
                .HasForeignKey(x => x.BaselineVersionId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.UserId, x.MatchId });
            e.HasIndex(x => new { x.AnonId, x.Ip, x.MatchId });
            // Chat mode: a refinement is either user-tied OR anon-tied — never both,
            // never neither. Keeps audit + quota lookups unambiguous.
            e.ToTable(t => t.HasCheckConstraint(
                "ck_refinement_identity",
                "(\"UserId\" IS NOT NULL) <> (\"AnonId\" IS NOT NULL)"));
        });

        b.Entity<AnonQuotaLedger>(e =>
        {
            e.HasKey(x => new { x.AnonId, x.Ip, x.QuotaDate });
        });

        b.Entity<PredictionSnapshot>(e =>
        {
            e.Property(x => x.OutcomeProbs).HasColumnType("jsonb");
            e.HasOne(x => x.Match).WithMany()
                .HasForeignKey(x => x.MatchId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Baseline).WithMany()
                .HasForeignKey(x => x.BaselineId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Refinement).WithMany()
                .HasForeignKey(x => x.RefinementId).OnDelete(DeleteBehavior.Cascade);
            // §15: polymorphic — exactly one of BaselineId / RefinementId is set.
            e.ToTable(t => t.HasCheckConstraint(
                "ck_snapshot_polymorphic",
                "(\"BaselineId\" IS NOT NULL) <> (\"RefinementId\" IS NOT NULL)"));
        });

        b.Entity<SnapshotScore>(e =>
        {
            e.HasKey(x => x.SnapshotId);
            e.HasOne(x => x.Snapshot).WithOne(s => s.Score)
                .HasForeignKey<SnapshotScore>(x => x.SnapshotId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.MatchResult).WithMany()
                .HasForeignKey(x => x.MatchResultId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Entitlement>(e =>
        {
            e.HasOne(x => x.User).WithMany(u => u.Entitlements)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.UserId, x.Status });
        });

        b.Entity<QuotaLedger>(e =>
        {
            e.HasKey(x => new { x.UserId, x.QuotaDate });
            e.HasOne(x => x.User).WithMany(u => u.QuotaLedger)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
