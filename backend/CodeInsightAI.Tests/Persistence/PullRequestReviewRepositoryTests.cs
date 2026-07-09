using CodeInsightAI.Domain.Entities;
using CodeInsightAI.Domain.Enums;
using CodeInsightAI.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CodeInsightAI.Tests.Persistence;

// Uses a real (in-memory) SQLite database rather than EF Core's InMemory provider, so the owned
// Issues collection, the JSON-converted Recommendations column, and cascade deletes behave exactly
// as they do against the real codeinsight.db file.
public class PullRequestReviewRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _dbContext;
    private readonly PullRequestReviewRepository _repository;

    public PullRequestReviewRepositoryTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new AppDbContext(options);
        _dbContext.Database.EnsureCreated();
        _repository = new PullRequestReviewRepository(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    private static PullRequestReport MakeReport(string headSha, int prNumber = 7, int score = 42)
    {
        return new PullRequestReport
        {
            RepoOwner = "sudenuryildirim",
            RepoName = "code-insight-ai",
            PrNumber = prNumber,
            PrTitle = "Test PR",
            PrUrl = "https://github.com/sudenuryildirim/code-insight-ai/pull/" + prNumber,
            Author = "sudenuryildirim",
            BaseBranch = "main",
            HeadBranch = "feature",
            HeadSha = headSha,
            DetectedPurpose = "Test amaçlı",
            ReliabilityScore = score,
            Verdict = "Riskli",
            Summary = "Özet",
            Issues = new List<ReviewIssue>
            {
                new ReviewIssue
                {
                    FilePath = "Foo.cs",
                    LineNumber = 10,
                    Severity = IssueSeverity.Critical,
                    Category = IssueCategory.Security,
                    Title = "SQL Injection",
                    Description = "...",
                    Suggestion = "...",
                },
            },
            Recommendations = new List<string> { "Parametreli sorgu kullan" },
        };
    }

    [Fact]
    public async Task SaveReviewAsync_persists_report_with_its_issues()
    {
        var report = MakeReport(headSha: "sha-1");

        await _repository.SaveReviewAsync(report);

        var saved = await _dbContext.PullRequestReports.Include(r => r.Issues).SingleAsync();
        Assert.Equal("sha-1", saved.HeadSha);
        Assert.Single(saved.Issues);
        Assert.Equal("SQL Injection", saved.Issues[0].Title);
        Assert.Equal(new[] { "Parametreli sorgu kullan" }, saved.Recommendations);
    }

    [Fact]
    public async Task GetLatestReviewAsync_returns_match_for_same_head_sha()
    {
        await _repository.SaveReviewAsync(MakeReport(headSha: "sha-1"));

        var result = await _repository.GetLatestReviewAsync("sudenuryildirim", "code-insight-ai", 7, "sha-1");

        Assert.NotNull(result);
        Assert.Equal("sha-1", result!.HeadSha);
    }

    [Fact]
    public async Task GetLatestReviewAsync_returns_null_when_head_sha_does_not_match()
    {
        await _repository.SaveReviewAsync(MakeReport(headSha: "sha-1"));

        var result = await _repository.GetLatestReviewAsync("sudenuryildirim", "code-insight-ai", 7, "sha-2");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetLatestReviewAsync_scopes_by_repo_owner_and_name()
    {
        await _repository.SaveReviewAsync(MakeReport(headSha: "sha-1"));

        var result = await _repository.GetLatestReviewAsync("someone-else", "code-insight-ai", 7, "sha-1");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetReviewHistoryAsync_returns_newest_first_and_caps_at_ten()
    {
        for (var i = 0; i < 12; i++)
        {
            var report = MakeReport(headSha: $"sha-{i}", score: i);
            report.CreatedAt = DateTime.UtcNow.AddMinutes(i); // ensure strictly increasing order
            await _repository.SaveReviewAsync(report);
        }

        var history = await _repository.GetReviewHistoryAsync("sudenuryildirim", "code-insight-ai", 7);

        Assert.Equal(10, history.Count);
        Assert.Equal("sha-11", history.First().HeadSha); // newest
        Assert.Equal("sha-2", history.Last().HeadSha); // 10th newest
    }
}
