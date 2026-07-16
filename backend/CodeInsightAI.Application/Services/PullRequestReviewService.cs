using System.Text;
using CodeInsightAI.Application.DTOs;
using CodeInsightAI.Application.Interfaces;
using CodeInsightAI.Domain.Entities;
using CodeInsightAI.Domain.Enums;

namespace CodeInsightAI.Application.Services;

public class PullRequestReviewService : IPullRequestReviewService
{
    private readonly IGitHubService _gitHubService;
    private readonly IAIService _aiService;
    private readonly IPullRequestReviewRepository _repository;

    public PullRequestReviewService(
        IGitHubService gitHubService,
        IAIService aiService,
        IPullRequestReviewRepository repository)
    {
        _gitHubService = gitHubService;
        _aiService = aiService;
        _repository = repository;
    }

    public Task<List<RepositoryRef>> GetConfiguredRepositoriesAsync()
    {
        return _gitHubService.GetConfiguredRepositoriesAsync();
    }

    public Task<List<PullRequestSummaryDto>> GetOpenPullRequestsAsync(string owner, string repo)
    {
        return _gitHubService.GetOpenPullRequestsAsync(owner, repo);
    }

    public async Task<PullRequestReport> ReviewPullRequestAsync(string owner, string repo, int prNumber, bool forceRefresh = false, string? customInstruction = null)
    {
        var context = await _gitHubService.GetPullRequestDiffAsync(owner, repo, prNumber);
        var hasCustomInstruction = !string.IsNullOrWhiteSpace(customInstruction);

        if (!forceRefresh && !hasCustomInstruction)
        {
            var cached = await _repository.GetLatestReviewAsync(context.RepoOwner, context.RepoName, prNumber, context.HeadSha);
            if (cached != null)
            {
                return cached;
            }
        }

        var report = await _aiService.AnalyzePullRequestAsync(context, customInstruction);

        // A failed analysis leaves HeadSha empty (see OllamaAIService) - never cache those,
        // otherwise a transient AI failure would get served forever for this commit.
        if (!string.IsNullOrEmpty(report.HeadSha))
        {
            await _repository.SaveReviewAsync(report);
        }

        return report;
    }

    public Task<List<PullRequestReport>> GetReviewHistoryAsync(string owner, string repo, int prNumber)
    {
        return _repository.GetReviewHistoryAsync(owner, repo, prNumber);
    }

    public async Task PostReviewCommentAsync(string owner, string repo, int prNumber, Guid reviewId)
    {
        var report = await _repository.GetByIdAsync(reviewId);
        if (report == null || report.RepoOwner != owner || report.RepoName != repo || report.PrNumber != prNumber)
        {
            throw new InvalidOperationException("Belirtilen inceleme kaydı bu PR için bulunamadı.");
        }

        var markdown = FormatReportAsMarkdown(report);
        await _gitHubService.PostReviewCommentAsync(owner, repo, prNumber, markdown);
    }

    private static string FormatReportAsMarkdown(PullRequestReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 🤖 CodeInsightAI İnceleme Raporu");
        sb.AppendLine();
        sb.AppendLine($"**Güvenilirlik Skoru:** {report.ReliabilityScore}/100 — {report.Verdict}");
        sb.AppendLine();
        sb.AppendLine(report.DetectedPurpose);

        if (report.Issues.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"### Bulgular ({report.Issues.Count})");

            foreach (var issue in report.Issues.OrderBy(SeverityRank))
            {
                sb.AppendLine();
                sb.AppendLine($"{SeverityEmoji(issue.Severity)} **{issue.Severity} — {issue.Title}** ({issue.Category}) — `{issue.FilePath}:{issue.LineNumber}`");
                sb.AppendLine();
                sb.AppendLine(issue.Description);
                sb.AppendLine();
                sb.AppendLine($"**Çözüm önerisi:** {issue.Suggestion}");
            }
        }

        if (report.Recommendations.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Genel Öneriler");
            foreach (var recommendation in report.Recommendations)
            {
                sb.AppendLine($"- {recommendation}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine("*Bu rapor CodeInsightAI tarafından otomatik üretilmiştir. Onay/merge kararı her zaman bir insana aittir.*");

        return sb.ToString();
    }

    private static int SeverityRank(ReviewIssue issue) => issue.Severity switch
    {
        IssueSeverity.Critical => 0,
        IssueSeverity.Error => 1,
        IssueSeverity.Warning => 2,
        _ => 3,
    };

    private static string SeverityEmoji(IssueSeverity severity) => severity switch
    {
        IssueSeverity.Critical => "🔴",
        IssueSeverity.Error => "🟠",
        IssueSeverity.Warning => "🟡",
        _ => "🔵",
    };
}
