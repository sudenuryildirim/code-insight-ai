using CodeInsightAI.Application.DTOs;
using CodeInsightAI.Domain.Entities;

namespace CodeInsightAI.Application.Interfaces;

public interface ICodeReviewService
{
    Task<CodeReviewReport> AnalyzeCodeAsync(ReviewRequestDto request);
}
