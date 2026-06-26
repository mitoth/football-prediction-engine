using Microsoft.EntityFrameworkCore;
using WcPredictions.Data;
using WcPredictions.PredictionEngine.Baseline;

namespace WcPredictions.PredictionEngine.Tests;

// No DB needed: ToQueryString() generates SQL without connecting. Proves the
// word-boundary Regex.IsMatch translates server-side to Postgres `~*` (rather
// than throwing on attempted client-eval).
public class RelevantArticlesSqlTests
{
    [Fact]
    public void RelevantArticles_translates_to_postgres_word_boundary_regex()
    {
        using var db = new WcDbContext(new DbContextOptionsBuilder<WcDbContext>()
            .UseNpgsql("Host=localhost;Database=x;Username=x;Password=x").Options);

        var sql = BaselineService.RelevantArticles(db, "Togo", "Mali").ToQueryString();

        Assert.Contains("~*", sql);            // case-insensitive regex operator
        Assert.Contains(@"\yTogo\y", sql);     // word-boundary anchored
        Assert.Contains(@"\yMali\y", sql);
    }
}
