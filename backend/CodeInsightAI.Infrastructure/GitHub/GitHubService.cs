using System.Net.Http.Headers;
using System.Text.Json;
using CodeInsightAI.Application.DTOs;
using CodeInsightAI.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace CodeInsightAI.Infrastructure.GitHub;

public class GitHubService : IGitHubService
{
    // Full file content is only fetched (for project-wide consistency checks) when the
    // PR touches a manageable number of files, to keep the AI prompt/token usage bounded.
    private const int MaxFilesForFullContent = 15;
    private const int MaxFileSizeBytesForFullContent = 50_000;

    private readonly HttpClient _httpClient;
    private readonly string _organization;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public GitHubService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;

        var token = configuration["GitHub:Token"];
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("GitHub:Token appsettings içinde yapılandırılmamış.");
        }

        _organization = configuration["GitHub:Organization"]
            ?? throw new InvalidOperationException("GitHub:Organization appsettings içinde yapılandırılmamış.");

        // Defaults to public GitHub's API host; set GitHub:ApiBaseUrl (e.g. "https://git.company.com/")
        // to point at a self-hosted GitHub Enterprise Server instance instead.
        var apiBaseUrl = configuration["GitHub:ApiBaseUrl"];
        _httpClient.BaseAddress = new Uri(NormalizeApiBaseUrl(apiBaseUrl));
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CodeInsightAI");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    // GitHub Enterprise Server's REST API lives under /api/v3, not at the domain root - add it
    // automatically if a bare host URL (e.g. "https://git.company.com") was configured, so a request
    // like "orgs/{org}/repos" doesn't silently hit the web UI (returning an HTML page instead of JSON,
    // which fails to deserialize) instead of the actual API.
    private static string NormalizeApiBaseUrl(string? apiBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            return "https://api.github.com/";
        }

        var trimmed = apiBaseUrl.TrimEnd('/');

        if (!trimmed.Contains("/api/v3", StringComparison.OrdinalIgnoreCase)
            && !trimmed.Contains("api.github.com", StringComparison.OrdinalIgnoreCase))
        {
            trimmed += "/api/v3";
        }

        return trimmed + "/";
    }

    // Auto-discovers every repo in the configured GitHub organization, rather than requiring each
    // repo to be listed by hand in config - so newly created org repos show up automatically.
    public async Task<List<RepositoryRef>> GetConfiguredRepositoriesAsync()
    {
        var response = await _httpClient.GetAsync($"orgs/{_organization}/repos?per_page=100&sort=updated");
        response.EnsureSuccessStatusCode();

        var repos = await response.Content.ReadFromJsonAsyncSafe<List<GitHubRepository>>(_jsonOptions) ?? new();

        return repos.Select(r => new RepositoryRef { Owner = r.Owner.Login, Repo = r.Name }).ToList();
    }

    public async Task<List<PullRequestSummaryDto>> GetOpenPullRequestsAsync(string owner, string repo)
    {
        var response = await _httpClient.GetAsync($"repos/{owner}/{repo}/pulls?state=open&sort=updated&direction=desc");
        response.EnsureSuccessStatusCode();

        var pulls = await response.Content.ReadFromJsonAsyncSafe<List<GitHubPullRequest>>(_jsonOptions) ?? new();

        return pulls.Select(pr => new PullRequestSummaryDto
        {
            Number = pr.Number,
            Title = pr.Title,
            Author = pr.User.Login,
            BaseBranch = pr.Base.Ref,
            HeadBranch = pr.Head.Ref,
            Url = pr.HtmlUrl,
            UpdatedAt = pr.UpdatedAt
        }).ToList();
    }

    public async Task<PullRequestDiffContext> GetPullRequestDiffAsync(string owner, string repo, int prNumber)
    {
        var prResponse = await _httpClient.GetAsync($"repos/{owner}/{repo}/pulls/{prNumber}");
        prResponse.EnsureSuccessStatusCode();
        var pr = await prResponse.Content.ReadFromJsonAsyncSafe<GitHubPullRequest>(_jsonOptions)
            ?? throw new InvalidOperationException($"PR #{prNumber} bulunamadı.");

        var filesResponse = await _httpClient.GetAsync($"repos/{owner}/{repo}/pulls/{prNumber}/files?per_page=100");
        filesResponse.EnsureSuccessStatusCode();
        var files = await filesResponse.Content.ReadFromJsonAsyncSafe<List<GitHubPullRequestFile>>(_jsonOptions) ?? new();

        var fetchFullContent = files.Count <= MaxFilesForFullContent;

        var fileChanges = new List<PullRequestFileChange>();
        foreach (var file in files)
        {
            var change = new PullRequestFileChange
            {
                FileName = file.FileName,
                Patch = file.Patch ?? "(binary dosya veya diff mevcut değil)"
            };

            if (fetchFullContent && file.Status != "removed")
            {
                change.FullContent = await TryGetFileContentAsync(owner, repo, file.FileName, pr.Head.Sha);
            }

            fileChanges.Add(change);
        }

        return new PullRequestDiffContext
        {
            RepoOwner = owner,
            RepoName = repo,
            PrNumber = pr.Number,
            PrTitle = pr.Title,
            PrDescription = pr.Body ?? string.Empty,
            PrUrl = pr.HtmlUrl,
            Author = pr.User.Login,
            BaseBranch = pr.Base.Ref,
            HeadBranch = pr.Head.Ref,
            HeadSha = pr.Head.Sha,
            Files = fileChanges,
            RawDiff = await TryGetRawDiffAsync(owner, repo, prNumber)
        };
    }

    // GitHub omits the per-file "patch" field once a PR's diff is large enough (common for PRs
    // touching dozens of files) - the files list still comes back, but every file's Patch is null,
    // which previously made it look to the AI like there was no diff at all despite the PR clearly
    // having one. Requesting the PR itself with the "diff" media type instead returns the full
    // unified diff as one plain-text block, which isn't subject to that per-file omission.
    private async Task<string> TryGetRawDiffAsync(string owner, string repo, int prNumber)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"repos/{owner}/{repo}/pulls/{prNumber}");
            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.diff"));

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return string.Empty;
            }

            return await response.Content.ReadAsStringAsync();
        }
        catch
        {
            // Best-effort - the per-file patches (and full file content) still give the AI something
            // to work with even if this fails (e.g. the PR's diff exceeds GitHub's size limit for it).
            return string.Empty;
        }
    }

    public async Task PostReviewCommentAsync(string owner, string repo, int prNumber, string markdownBody)
    {
        // GitHub treats PRs as issues for the purposes of plain (non-diff-line) comments.
        var payload = JsonSerializer.Serialize(new { body = markdownBody });
        var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"repos/{owner}/{repo}/issues/{prNumber}/comments", content);
        response.EnsureSuccessStatusCode();
    }

    private async Task<string> TryGetFileContentAsync(string owner, string repo, string path, string headSha)
    {
        try
        {
            var response = await _httpClient.GetAsync($"repos/{owner}/{repo}/contents/{Uri.EscapeDataString(path)}?ref={headSha}");
            if (!response.IsSuccessStatusCode)
            {
                return string.Empty;
            }

            var content = await response.Content.ReadFromJsonAsyncSafe<GitHubContentResponse>(_jsonOptions);
            if (content?.Content == null || content.Encoding != "base64")
            {
                return string.Empty;
            }

            var bytes = Convert.FromBase64String(content.Content.Replace("\n", string.Empty));
            if (bytes.Length > MaxFileSizeBytesForFullContent)
            {
                return string.Empty;
            }

            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            // Full content is a best-effort enrichment for consistency checks; the diff alone is enough to proceed.
            return string.Empty;
        }
    }
}

internal static class HttpContentJsonExtensions
{
    public static async Task<T?> ReadFromJsonAsyncSafe<T>(this HttpContent content, JsonSerializerOptions options)
    {
        var stream = await content.ReadAsStreamAsync();
        return await JsonSerializer.DeserializeAsync<T>(stream, options);
    }
}
