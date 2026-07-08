using CodeInsightAI.Application.DTOs;
using CodeInsightAI.Application.Interfaces;
using CodeInsightAI.Domain.Entities;

namespace CodeInsightAI.Application.Services;

public class CodeReviewService : ICodeReviewService
{
    private readonly IAIService _aiService;

    public CodeReviewService(IAIService aiService)
    {
        _aiService = aiService;
    }

    public async Task<CodeReviewReport> AnalyzeCodeAsync(ReviewRequestDto request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return new CodeReviewReport
            {
                FileName = request.FileName,
                Summary = "Gönderilen kod boş. Lütfen analiz edilmesini istediğiniz kodu girin.",
                Issues = new List<ReviewIssue>()
            };
        }

        return await _aiService.AnalyzeCodeAsync(request.Code, request.FileName);
    }
}
