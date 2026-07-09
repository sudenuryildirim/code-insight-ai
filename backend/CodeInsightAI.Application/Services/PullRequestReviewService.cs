using CodeInsightAI.Application.DTOs;
using CodeInsightAI.Application.Interfaces;
using CodeInsightAI.Domain.Entities;

namespace CodeInsightAI.Application.Services;

public class PullRequestReviewService : IPullRequestReviewService
{
    private readonly IGitHubService _gitHubService;
    private readonly IAIService _aiService;
    private readonly IPullRequestReviewRepository _repository;

    public PullRequestReviewService(
        IGitHubService gitHubService,
        IAIService aiService,
        IPullRequestReviewRepository repository)
    {
        _gitHubService = gitHubService;
        _aiService = aiService;
        _repository = repository;
    }

    public List<RepositoryRef> GetConfiguredRepositories()
    {
        return _gitHubService.GetConfiguredRepositories();
    }

    public Task<List<PullRequestSummaryDto>> GetOpenPullRequestsAsync(string owner, string repo)
    {
        return _gitHubService.GetOpenPullRequestsAsync(owner, repo);
    }

    public async Task<PullRequestReport> ReviewPullRequestAsync(string owner, string repo, int prNumber, bool forceRefresh = false)
    {
        var context = await _gitHubService.GetPullRequestDiffAsync(owner, repo, prNumber);

        if (!forceRefresh)
        {
            var cached = await _repository.GetLatestReviewAsync(context.RepoOwner, context.RepoName, prNumber, context.HeadSha);
            if (cached != null)
            {
                return cached;
            }
        }

        var report = await _aiService.AnalyzePullRequestAsync(context);

        // A failed analysis leaves HeadSha empty (see GeminiAIService) - never cache those,
        // otherwise a transient Gemini failure would get served forever for this commit.
        if (!string.IsNullOrEmpty(report.HeadSha))
        {
            await _repository.SaveReviewAsync(report);
        }

        return report;
    }

    public Task<List<PullRequestReport>> GetReviewHistoryAsync(string owner, string repo, int prNumber)
    {
        return _repository.GetReviewHistoryAsync(owner, repo, prNumber);
    }
}
