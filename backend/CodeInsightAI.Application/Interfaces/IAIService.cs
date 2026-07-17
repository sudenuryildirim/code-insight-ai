using CodeInsightAI.Application.DTOs;
using CodeInsightAI.Domain.Entities;

namespace CodeInsightAI.Application.Interfaces;

public interface IAIService
{
    Task<PullRequestReport> AnalyzePullRequestAsync(PullRequestDiffContext context);
}
