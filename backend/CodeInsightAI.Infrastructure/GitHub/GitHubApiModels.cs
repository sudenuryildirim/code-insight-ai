using System.Text.Json.Serialization;

namespace CodeInsightAI.Infrastructure.GitHub;

internal class GitHubUser
{
    [JsonPropertyName("login")]
    public string Login { get; set; } = string.Empty;
}

internal class GitHubRepository
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("owner")]
    public GitHubUser Owner { get; set; } = new();
}

internal class GitHubBranchRef
{
    [JsonPropertyName("ref")]
    public string Ref { get; set; } = string.Empty;

    [JsonPropertyName("sha")]
    public string Sha { get; set; } = string.Empty;
}

internal class GitHubPullRequest
{
    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    [JsonPropertyName("user")]
    public GitHubUser User { get; set; } = new();

    [JsonPropertyName("base")]
    public GitHubBranchRef Base { get; set; } = new();

    [JsonPropertyName("head")]
    public GitHubBranchRef Head { get; set; } = new();

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

internal class GitHubPullRequestFile
{
    [JsonPropertyName("filename")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("patch")]
    public string? Patch { get; set; }

    [JsonPropertyName("changes")]
    public int Changes { get; set; }
}

internal class GitHubContentResponse
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("encoding")]
    public string? Encoding { get; set; }
}
