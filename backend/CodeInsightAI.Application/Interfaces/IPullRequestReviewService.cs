using CodeInsightAI.Application.DTOs;
using CodeInsightAI.Domain.Entities;

namespace CodeInsightAI.Application.Interfaces;

public interface IPullRequestReviewService
{
    List<RepositoryRef> GetConfiguredRepositories();

    Task<List<PullRequestSummaryDto>> GetOpenPullRequestsAsync(string owner, string repo);

    Task<PullRequestReport> ReviewPullRequestAsync(string owner, string repo, int prNumber, bool forceRefresh = false);

    Task<List<PullRequestReport>> GetReviewHistoryAsync(string owner, string repo, int prNumber);
}
