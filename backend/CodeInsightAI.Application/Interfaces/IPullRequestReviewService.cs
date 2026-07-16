using CodeInsightAI.Application.DTOs;
using CodeInsightAI.Domain.Entities;

namespace CodeInsightAI.Application.Interfaces;

public interface IPullRequestReviewService
{
    Task<List<RepositoryRef>> GetConfiguredRepositoriesAsync();

    Task<List<PullRequestSummaryDto>> GetOpenPullRequestsAsync(string owner, string repo);

    // customInstruction lets the user steer this specific analysis (e.g. "sadece güvenlik açıklarına
    // odaklan") on top of the standard checklist. Providing one always calls the AI fresh, the same
    // as forceRefresh - a custom-instruction result isn't what a plain "İncele" click should get back.
    Task<PullRequestReport> ReviewPullRequestAsync(string owner, string repo, int prNumber, bool forceRefresh = false, string? customInstruction = null);

    Task<List<PullRequestReport>> GetReviewHistoryAsync(string owner, string repo, int prNumber);

    // Posts the given past review as a plain comment on the PR itself. Never approves or merges -
    // it only makes the report visible to people who don't have this app open.
    Task PostReviewCommentAsync(string owner, string repo, int prNumber, Guid reviewId);
}
