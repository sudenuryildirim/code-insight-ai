using CodeInsightAI.Domain.Enums;

namespace CodeInsightAI.Domain.Entities;

public class CodeReviewReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Programming language, automatically detected by the AI from the source code
    /// (the user is never asked to pick it).
    /// </summary>
    public string DetectedLanguage { get; set; } = string.Empty;

    public int Score { get; set; } // Quality score from 0 to 100

    /// <summary>
    /// Detailed explanation of what the code does / is meant to do.
    /// </summary>
    public string CodePurpose { get; set; } = string.Empty;

    /// <summary>
    /// General, detailed assessment of the overall quality of the code.
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Things the code already does well.
    /// </summary>
    public List<string> Strengths { get; set; } = new();

    public List<ReviewIssue> Issues { get; set; } = new();

    /// <summary>
    /// General, high-level improvement recommendations that go beyond individual issues
    /// (architecture, testing strategy, tooling, etc.).
    /// </summary>
    public List<string> Recommendations { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
