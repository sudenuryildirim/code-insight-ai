using CodeInsightAI.Domain.Entities;

namespace CodeInsightAI.Application.Interfaces;

public interface IPullRequestReviewRepository
{
    Task<PullRequestReport?> GetLatestReviewAsync(string owner, string repo, int prNumber, string headSha);

    Task<PullRequestReport?> GetByIdAsync(Guid id);

    Task<List<PullRequestReport>> GetReviewHistoryAsync(string owner, string repo, int prNumber);

    Task SaveReviewAsync(PullRequestReport report);
}
