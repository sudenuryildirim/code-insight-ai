namespace CodeInsightAI.Domain.Entities;

public class PullRequestReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RepoOwner { get; set; } = string.Empty;
    public string RepoName { get; set; } = string.Empty;
    public int PrNumber { get; set; }
    public string PrTitle { get; set; } = string.Empty;
    public string PrUrl { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string BaseBranch { get; set; } = string.Empty;
    public string HeadBranch { get; set; } = string.Empty;

    /// <summary>
    /// What this PR is trying to achieve, inferred by the AI from the title, description and diff.
    /// </summary>
    public string DetectedPurpose { get; set; } = string.Empty;

    public int ReliabilityScore { get; set; } // 0-100

    /// <summary>
    /// Short human-readable verdict, e.g. "Onaya Hazır" / "Değişiklik Gerekli" / "Riskli".
    /// The merge/approve decision itself is always made by a human on GitHub.
    /// </summary>
    public string Verdict { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;
    public List<ReviewIssue> Issues { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
