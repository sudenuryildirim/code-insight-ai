using CodeInsightAI.Domain.Entities;

namespace CodeInsightAI.Application.Interfaces;

/// <summary>
/// Renders a CodeReviewReport into a downloadable PDF document.
/// </summary>
public interface IPdfReportGenerator
{
    byte[] Generate(CodeReviewReport report);
}
