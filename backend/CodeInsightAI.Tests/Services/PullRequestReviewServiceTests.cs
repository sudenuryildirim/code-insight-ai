using CodeInsightAI.Application.DTOs;
using CodeInsightAI.Application.Interfaces;
using CodeInsightAI.Application.Services;
using CodeInsightAI.Domain.Entities;
using CodeInsightAI.Domain.Enums;
using Moq;

namespace CodeInsightAI.Tests.Services;

public class PullRequestReviewServiceTests
{
    private readonly Mock<IGitHubService> _gitHubService = new();
    private readonly Mock<IAIService> _aiService = new();
    private readonly Mock<IPullRequestReviewRepository> _repository = new();
    private readonly PullRequestReviewService _service;

    private const string Owner = "acme-corp";
    private const string Repo = "code-insight-ai";

    public PullRequestReviewServiceTests()
    {
        _service = new PullRequestReviewService(_gitHubService.Object, _aiService.Object, _repository.Object);
    }

    private static PullRequestDiffContext MakeContext(int prNumber, string headSha)
    {
        return new PullRequestDiffContext
        {
            RepoOwner = Owner,
            RepoName = Repo,
            PrNumber = prNumber,
            HeadSha = headSha,
            Files = new List<PullRequestFileChange>(),
        };
    }

    [Fact]
    public async Task ReviewPullRequestAsync_returns_cached_report_and_never_calls_AI_when_head_sha_matches()
    {
        var context = MakeContext(prNumber: 5, headSha: "sha-1");
        var cached = new PullRequestReport { HeadSha = "sha-1", ReliabilityScore = 88 };

        _gitHubService.Setup(g => g.GetPullRequestDiffAsync(Owner, Repo, 5)).ReturnsAsync(context);
        _repository.Setup(r => r.GetLatestReviewAsync(Owner, Repo, 5, "sha-1")).ReturnsAsync(cached);

        var result = await _service.ReviewPullRequestAsync(Owner, Repo, 5);

        Assert.Same(cached, result);
        _aiService.Verify(a => a.AnalyzePullRequestAsync(It.IsAny<PullRequestDiffContext>()), Times.Never);
        _repository.Verify(r => r.SaveReviewAsync(It.IsAny<PullRequestReport>()), Times.Never);
    }

    [Fact]
    public async Task ReviewPullRequestAsync_calls_AI_and_saves_when_no_cached_review_exists()
    {
        var context = MakeContext(prNumber: 5, headSha: "sha-1");
        var freshReport = new PullRequestReport { HeadSha = "sha-1", ReliabilityScore = 40 };

        _gitHubService.Setup(g => g.GetPullRequestDiffAsync(Owner, Repo, 5)).ReturnsAsync(context);
        _repository.Setup(r => r.GetLatestReviewAsync(Owner, Repo, 5, "sha-1")).ReturnsAsync((PullRequestReport?)null);
        _aiService.Setup(a => a.AnalyzePullRequestAsync(context)).ReturnsAsync(freshReport);

        var result = await _service.ReviewPullRequestAsync(Owner, Repo, 5);

        Assert.Same(freshReport, result);
        _repository.Verify(r => r.SaveReviewAsync(freshReport), Times.Once);
    }

    [Fact]
    public async Task ReviewPullRequestAsync_with_forceRefresh_skips_cache_lookup_and_calls_AI_anyway()
    {
        var context = MakeContext(prNumber: 5, headSha: "sha-1");
        var freshReport = new PullRequestReport { HeadSha = "sha-1", ReliabilityScore = 77 };

        _gitHubService.Setup(g => g.GetPullRequestDiffAsync(Owner, Repo, 5)).ReturnsAsync(context);
        _aiService.Setup(a => a.AnalyzePullRequestAsync(context)).ReturnsAsync(freshReport);

        var result = await _service.ReviewPullRequestAsync(Owner, Repo, 5, forceRefresh: true);

        Assert.Same(freshReport, result);
        _repository.Verify(r => r.GetLatestReviewAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        _repository.Verify(r => r.SaveReviewAsync(freshReport), Times.Once);
    }

    [Fact]
    public async Task ReviewPullRequestAsync_does_not_cache_a_failed_analysis()
    {
        var context = MakeContext(prNumber: 5, headSha: "sha-1");
        // GeminiAIService leaves HeadSha empty on its error-fallback report - simulate that here.
        var failedReport = new PullRequestReport { HeadSha = string.Empty, ReliabilityScore = 0, Verdict = "Analiz Başarısız" };

        _gitHubService.Setup(g => g.GetPullRequestDiffAsync(Owner, Repo, 5)).ReturnsAsync(context);
        _repository.Setup(r => r.GetLatestReviewAsync(Owner, Repo, 5, "sha-1")).ReturnsAsync((PullRequestReport?)null);
        _aiService.Setup(a => a.AnalyzePullRequestAsync(context)).ReturnsAsync(failedReport);

        var result = await _service.ReviewPullRequestAsync(Owner, Repo, 5);

        Assert.Same(failedReport, result);
        _repository.Verify(r => r.SaveReviewAsync(It.IsAny<PullRequestReport>()), Times.Never);
    }

    [Fact]
    public async Task GetReviewHistoryAsync_delegates_to_repository()
    {
        var history = new List<PullRequestReport> { new() { PrNumber = 9 } };
        _repository.Setup(r => r.GetReviewHistoryAsync(Owner, Repo, 9)).ReturnsAsync(history);

        var result = await _service.GetReviewHistoryAsync(Owner, Repo, 9);

        Assert.Same(history, result);
    }

    [Fact]
    public async Task GetConfiguredRepositoriesAsync_delegates_to_github_service()
    {
        var repos = new List<RepositoryRef> { new() { Owner = Owner, Repo = Repo } };
        _gitHubService.Setup(g => g.GetConfiguredRepositoriesAsync()).ReturnsAsync(repos);

        var result = await _service.GetConfiguredRepositoriesAsync();

        Assert.Same(repos, result);
    }

    [Fact]
    public async Task PostReviewCommentAsync_posts_formatted_markdown_when_review_matches_pr()
    {
        var reviewId = Guid.NewGuid();
        var report = new PullRequestReport
        {
            Id = reviewId,
            RepoOwner = Owner,
            RepoName = Repo,
            PrNumber = 5,
            ReliabilityScore = 42,
            Verdict = "Değişiklik Gerekli",
            DetectedPurpose = "Test amaçlı bir PR.",
            Issues = new List<ReviewIssue>
            {
                new() { Title = "SQL Injection", Severity = IssueSeverity.Critical, Category = IssueCategory.Security, FilePath = "Foo.cs", LineNumber = 10, Description = "...", Suggestion = "Parametreli sorgu kullan." },
            },
            Recommendations = new List<string> { "Genel bir öneri." },
        };
        _repository.Setup(r => r.GetByIdAsync(reviewId)).ReturnsAsync(report);

        await _service.PostReviewCommentAsync(Owner, Repo, 5, reviewId);

        _gitHubService.Verify(g => g.PostReviewCommentAsync(
            Owner, Repo, 5,
            It.Is<string>(body => body.Contains("42/100") && body.Contains("SQL Injection") && body.Contains("Parametreli sorgu kullan."))),
            Times.Once);
    }

    [Fact]
    public async Task PostReviewCommentAsync_throws_when_review_not_found()
    {
        var reviewId = Guid.NewGuid();
        _repository.Setup(r => r.GetByIdAsync(reviewId)).ReturnsAsync((PullRequestReport?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.PostReviewCommentAsync(Owner, Repo, 5, reviewId));

        _gitHubService.Verify(g => g.PostReviewCommentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PostReviewCommentAsync_throws_when_review_belongs_to_a_different_pr()
    {
        var reviewId = Guid.NewGuid();
        var report = new PullRequestReport { Id = reviewId, RepoOwner = Owner, RepoName = Repo, PrNumber = 99 };
        _repository.Setup(r => r.GetByIdAsync(reviewId)).ReturnsAsync(report);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.PostReviewCommentAsync(Owner, Repo, 5, reviewId));

        _gitHubService.Verify(g => g.PostReviewCommentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }
}
