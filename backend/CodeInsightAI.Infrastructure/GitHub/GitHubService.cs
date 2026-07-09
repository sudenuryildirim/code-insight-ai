using System.Net.Http.Headers;
using System.Text.Json;
using CodeInsightAI.Application.DTOs;
using CodeInsightAI.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace CodeInsightAI.Infrastructure.GitHub;

public class GitHubService : IGitHubService
{
    // Full file content is only fetched (for project-wide consistency checks) when the
    // PR touches a manageable number of files, to keep the Gemini prompt/token usage bounded.
    private const int MaxFilesForFullContent = 15;
    private const int MaxFileSizeBytesForFullContent = 50_000;

    private readonly HttpClient _httpClient;
    private readonly List<RepositoryRef> _repositories;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public GitHubService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;

        var token = configuration["GitHub:Token"] ?? throw new InvalidOperationException("GitHub token is not configured.");
        _repositories = configuration.GetSection("GitHub:Repositories").Get<List<RepositoryRef>>() ?? new();

        _httpClient.BaseAddress = new Uri("https://api.github.com/");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CodeInsightAI");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }

    public List<RepositoryRef> GetConfiguredRepositories() => _repositories;

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
            Files = fileChanges
        };
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
