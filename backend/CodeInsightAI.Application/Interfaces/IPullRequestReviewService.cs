using CodeInsightAI.Application.DTOs;
using CodeInsightAI.Domain.Entities;

namespace CodeInsightAI.Application.Interfaces;

public interface IPullRequestReviewService
{
    Task<List<PullRequestSummaryDto>> GetOpenPullRequestsAsync();

    Task<PullRequestReport> ReviewPullRequestAsync(int prNumber);
}
