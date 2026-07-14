using CodeInsightAI.Application.DTOs;

namespace CodeInsightAI.Application.Interfaces;

public interface IGitHubService
{
    // Auto-discovers every repo in the configured GitHub organization.
    Task<List<RepositoryRef>> GetConfiguredRepositoriesAsync();

    Task<List<PullRequestSummaryDto>> GetOpenPullRequestsAsync(string owner, string repo);

    Task<PullRequestDiffContext> GetPullRequestDiffAsync(string owner, string repo, int prNumber);

    // Posts a plain issue-style comment on the PR (not tied to a specific diff line). Requires a
    // token with write access to the repo (classic PAT: "repo", or "public_repo" for public repos).
    Task PostReviewCommentAsync(string owner, string repo, int prNumber, string markdownBody);
}
