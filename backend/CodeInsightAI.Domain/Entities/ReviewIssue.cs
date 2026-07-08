using CodeInsightAI.Domain.Enums;

namespace CodeInsightAI.Domain.Entities;

public class ReviewIssue
{
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string LineContent { get; set; } = string.Empty;
    public IssueSeverity Severity { get; set; }
    public IssueCategory Category { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Suggestion { get; set; } = string.Empty;
    public string RefactoredCode { get; set; } = string.Empty;
}
