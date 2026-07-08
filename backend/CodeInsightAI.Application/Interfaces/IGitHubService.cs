using CodeInsightAI.Application.DTOs;

namespace CodeInsightAI.Application.Interfaces;

public interface IGitHubService
{
    Task<List<PullRequestSummaryDto>> GetOpenPullRequestsAsync();

    Task<PullRequestDiffContext> GetPullRequestDiffAsync(int prNumber);
}
