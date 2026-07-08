using CodeInsightAI.Application.DTOs;
using CodeInsightAI.Application.Interfaces;
using CodeInsightAI.Domain.Entities;

namespace CodeInsightAI.Application.Services;

public class PullRequestReviewService : IPullRequestReviewService
{
    private readonly IGitHubService _gitHubService;
    private readonly IAIService _aiService;

    public PullRequestReviewService(IGitHubService gitHubService, IAIService aiService)
    {
        _gitHubService = gitHubService;
        _aiService = aiService;
    }

    public Task<List<PullRequestSummaryDto>> GetOpenPullRequestsAsync()
    {
        return _gitHubService.GetOpenPullRequestsAsync();
    }

    public async Task<PullRequestReport> ReviewPullRequestAsync(int prNumber)
    {
        var context = await _gitHubService.GetPullRequestDiffAsync(prNumber);
        return await _aiService.AnalyzePullRequestAsync(context);
    }
}
