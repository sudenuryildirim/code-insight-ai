using CodeInsightAI.Application.DTOs;

namespace CodeInsightAI.Application.Interfaces;

public interface IGitHubService
{
    List<RepositoryRef> GetConfiguredRepositories();

    Task<List<PullRequestSummaryDto>> GetOpenPullRequestsAsync(string owner, string repo);

    Task<PullRequestDiffContext> GetPullRequestDiffAsync(string owner, string repo, int prNumber);
}
