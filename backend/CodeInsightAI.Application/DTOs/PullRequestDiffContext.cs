namespace CodeInsightAI.Application.DTOs;

public class PullRequestFileChange
{
    public string FileName { get; set; } = string.Empty;
    public string Patch { get; set; } = string.Empty;

    /// <summary>
    /// Full content of the file after the change, when it was small enough to fetch
    /// (used for project-wide consistency checks). Empty if not fetched.
    /// </summary>
    public string FullContent { get; set; } = string.Empty;
}

/// <summary>
/// Everything the AI needs to review a pull request: metadata plus the changed files.
/// </summary>
public class PullRequestDiffContext
{
    public string RepoOwner { get; set; } = string.Empty;
    public string RepoName { get; set; } = string.Empty;
    public int PrNumber { get; set; }
    public string PrTitle { get; set; } = string.Empty;
    public string PrDescription { get; set; } = string.Empty;
    public string PrUrl { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string BaseBranch { get; set; } = string.Empty;
    public string HeadBranch { get; set; } = string.Empty;
    public string HeadSha { get; set; } = string.Empty;
    public List<PullRequestFileChange> Files { get; set; } = new();
}
