using CodeInsightAI.Application.DTOs;
using CodeInsightAI.Application.Interfaces;
using CodeInsightAI.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace CodeInsightAI.Application.Services;

public class PullRequestReviewService : IPullRequestReviewService
{
    private readonly IGitHubService _gitHubService;
    private readonly IAIService _aiService;
    private readonly IPullRequestReviewRepository _repository;
    private readonly string _owner;
    private readonly string _repo;

    public PullRequestReviewService(
        IGitHubService gitHubService,
        IAIService aiService,
        IPullRequestReviewRepository repository,
        IConfiguration configuration)
    {
        _gitHubService = gitHubService;
        _aiService = aiService;
        _repository = repository;
        _owner = configuration["GitHub:Owner"] ?? string.Empty;
        _repo = configuration["GitHub:Repo"] ?? string.Empty;
    }

    public Task<List<PullRequestSummaryDto>> GetOpenPullRequestsAsync()
    {
        return _gitHubService.GetOpenPullRequestsAsync();
    }

    public async Task<PullRequestReport> ReviewPullRequestAsync(int prNumber, bool forceRefresh = false)
    {
        var context = await _gitHubService.GetPullRequestDiffAsync(prNumber);

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

    public Task<List<PullRequestReport>> GetReviewHistoryAsync(int prNumber)
    {
        return _repository.GetReviewHistoryAsync(_owner, _repo, prNumber);
    }
}
