using CodeInsightAI.Application.DTOs;
using CodeInsightAI.Application.Interfaces;
using CodeInsightAI.Application.Services;
using CodeInsightAI.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Moq;

namespace CodeInsightAI.Tests.Services;

public class PullRequestReviewServiceTests
{
    private readonly Mock<IGitHubService> _gitHubService = new();
    private readonly Mock<IAIService> _aiService = new();
    private readonly Mock<IPullRequestReviewRepository> _repository = new();
    private readonly PullRequestReviewService _service;

    private const string Owner = "sudenuryildirim";
    private const string Repo = "code-insight-ai";

    public PullRequestReviewServiceTests()
    {
        var configuration = new Mock<IConfiguration>();
        configuration.Setup(c => c["GitHub:Owner"]).Returns(Owner);
        configuration.Setup(c => c["GitHub:Repo"]).Returns(Repo);

        _service = new PullRequestReviewService(_gitHubService.Object, _aiService.Object, _repository.Object, configuration.Object);
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

        _gitHubService.Setup(g => g.GetPullRequestDiffAsync(5)).ReturnsAsync(context);
        _repository.Setup(r => r.GetLatestReviewAsync(Owner, Repo, 5, "sha-1")).ReturnsAsync(cached);

        var result = await _service.ReviewPullRequestAsync(5);

        Assert.Same(cached, result);
        _aiService.Verify(a => a.AnalyzePullRequestAsync(It.IsAny<PullRequestDiffContext>()), Times.Never);
        _repository.Verify(r => r.SaveReviewAsync(It.IsAny<PullRequestReport>()), Times.Never);
    }

    [Fact]
    public async Task ReviewPullRequestAsync_calls_AI_and_saves_when_no_cached_review_exists()
    {
        var context = MakeContext(prNumber: 5, headSha: "sha-1");
        var freshReport = new PullRequestReport { HeadSha = "sha-1", ReliabilityScore = 40 };

        _gitHubService.Setup(g => g.GetPullRequestDiffAsync(5)).ReturnsAsync(context);
        _repository.Setup(r => r.GetLatestReviewAsync(Owner, Repo, 5, "sha-1")).ReturnsAsync((PullRequestReport?)null);
        _aiService.Setup(a => a.AnalyzePullRequestAsync(context)).ReturnsAsync(freshReport);

        var result = await _service.ReviewPullRequestAsync(5);

        Assert.Same(freshReport, result);
        _repository.Verify(r => r.SaveReviewAsync(freshReport), Times.Once);
    }

    [Fact]
    public async Task ReviewPullRequestAsync_with_forceRefresh_skips_cache_lookup_and_calls_AI_anyway()
    {
        var context = MakeContext(prNumber: 5, headSha: "sha-1");
        var freshReport = new PullRequestReport { HeadSha = "sha-1", ReliabilityScore = 77 };

        _gitHubService.Setup(g => g.GetPullRequestDiffAsync(5)).ReturnsAsync(context);
        _aiService.Setup(a => a.AnalyzePullRequestAsync(context)).ReturnsAsync(freshReport);

        var result = await _service.ReviewPullRequestAsync(5, forceRefresh: true);

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

        _gitHubService.Setup(g => g.GetPullRequestDiffAsync(5)).ReturnsAsync(context);
        _repository.Setup(r => r.GetLatestReviewAsync(Owner, Repo, 5, "sha-1")).ReturnsAsync((PullRequestReport?)null);
        _aiService.Setup(a => a.AnalyzePullRequestAsync(context)).ReturnsAsync(failedReport);

        var result = await _service.ReviewPullRequestAsync(5);

        Assert.Same(failedReport, result);
        _repository.Verify(r => r.SaveReviewAsync(It.IsAny<PullRequestReport>()), Times.Never);
    }

    [Fact]
    public async Task GetReviewHistoryAsync_delegates_to_repository_with_configured_owner_and_repo()
    {
        var history = new List<PullRequestReport> { new() { PrNumber = 9 } };
        _repository.Setup(r => r.GetReviewHistoryAsync(Owner, Repo, 9)).ReturnsAsync(history);

        var result = await _service.GetReviewHistoryAsync(9);

        Assert.Same(history, result);
    }
}
