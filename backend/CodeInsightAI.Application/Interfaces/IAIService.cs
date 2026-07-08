using CodeInsightAI.Application.DTOs;
using CodeInsightAI.Domain.Entities;

namespace CodeInsightAI.Application.Interfaces;

public interface IAIService
{
    Task<CodeReviewReport> AnalyzeCodeAsync(string code, string filename);

    Task<PullRequestReport> AnalyzePullRequestAsync(PullRequestDiffContext context);
}
