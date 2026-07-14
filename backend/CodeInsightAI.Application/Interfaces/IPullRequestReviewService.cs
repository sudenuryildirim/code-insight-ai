using CodeInsightAI.Application.DTOs;
using CodeInsightAI.Domain.Entities;

namespace CodeInsightAI.Application.Interfaces;

public interface IPullRequestReviewService
{
    Task<List<RepositoryRef>> GetConfiguredRepositoriesAsync();

    Task<List<PullRequestSummaryDto>> GetOpenPullRequestsAsync(string owner, string repo);

    Task<PullRequestReport> ReviewPullRequestAsync(string owner, string repo, int prNumber, bool forceRefresh = false);

    Task<List<PullRequestReport>> GetReviewHistoryAsync(string owner, string repo, int prNumber);

    // Posts the given past review as a plain comment on the PR itself. Never approves or merges -
    // it only makes the report visible to people who don't have this app open.
    Task PostReviewCommentAsync(string owner, string repo, int prNumber, Guid reviewId);
}
