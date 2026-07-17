using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeInsightAI.Application.AI;
using CodeInsightAI.Application.DTOs;
using CodeInsightAI.Application.Interfaces;
using CodeInsightAI.Domain.Entities;
using CodeInsightAI.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace CodeInsightAI.Infrastructure.AI;

public class GeminiAIService : IAIService
{
    private readonly HttpClient _httpClient;
    private readonly ISystemPromptRepository _systemPromptRepository;
    private readonly string _apiKey;
    private readonly string _model;

    public GeminiAIService(HttpClient httpClient, ISystemPromptRepository systemPromptRepository, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _systemPromptRepository = systemPromptRepository;
        _apiKey = configuration["Gemini:ApiKey"] ?? throw new InvalidOperationException("Gemini API key is not configured.");
        _model = configuration["Gemini:Model"] ?? "gemini-2.5-flash";
    }

    public async Task<PullRequestReport> AnalyzePullRequestAsync(PullRequestDiffContext context)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";

        // Editable from the app's settings screen instead of being hardcoded - falls back to the
        // built-in default until the user customizes it.
        var systemPrompt = await _systemPromptRepository.GetCustomPromptAsync() ?? DefaultSystemPrompt.Text;

        var filesSection = new StringBuilder();
        foreach (var file in context.Files)
        {
            filesSection.AppendLine($"### Dosya: {file.FileName}");
            filesSection.AppendLine("Diff (patch):");
            filesSection.AppendLine("```diff");
            filesSection.AppendLine(Truncate(file.Patch, 8000));
            filesSection.AppendLine("```");

            if (!string.IsNullOrWhiteSpace(file.FullContent))
            {
                filesSection.AppendLine("PR sonrası tam dosya içeriği (tutarlılık analizi için bağlam):");
                filesSection.AppendLine("```");
                filesSection.AppendLine(Truncate(file.FullContent, 15000));
                filesSection.AppendLine("```");
            }

            filesSection.AppendLine();
        }

        var rawDiffSection = string.IsNullOrWhiteSpace(context.RawDiff)
            ? string.Empty
            : $@"PR'ın tam birleştirilmiş diff'i (dosya bazlı patch'ler eksik/kesilmiş olsa bile bu bölüm PR'daki
gerçek değişikliklerin eksiksiz halidir - analizini öncelikle buna dayandır):
```diff
{Truncate(context.RawDiff, 60000)}
```

";

        var promptText = $@"Repo: {context.RepoOwner}/{context.RepoName}
PR #{context.PrNumber}: {context.PrTitle}
Yazan: {context.Author}
Branch: {context.HeadBranch} -> {context.BaseBranch}

PR Açıklaması:
{(string.IsNullOrWhiteSpace(context.PrDescription) ? "(açıklama girilmemiş)" : context.PrDescription)}

{rawDiffSection}Değişen dosyalar ({context.Files.Count} adet):

{filesSection}";

        var requestBody = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = promptText } } }
            },
            systemInstruction = new
            {
                parts = new[] { new { text = systemPrompt } }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                maxOutputTokens = 32768,
                responseSchema = new
                {
                    type = "OBJECT",
                    properties = new
                    {
                        detectedPurpose = new { type = "STRING", description = "PR'ın ne yapmaya çalıştığını anlatan, en az 1-2 uzun paragraftan oluşan detaylı Türkçe açıklama" },
                        reliabilityScore = new { type = "INTEGER", description = "PR'ın genel güvenilirlik skoru (0-100 arası)" },
                        verdict = new { type = "STRING", description = "Bulguları özetleyen kısa etiket, örn. 'Onaya Hazır Görünüyor', 'Değişiklik Gerekli'. Bu bir onay/red kararı DEĞİLDİR, sadece özet." },
                        summary = new { type = "STRING", description = "PR'ın genel değerlendirmesini, risklerini ve tutarlılığını özetleyen uzun Türkçe değerlendirme" },
                        issues = new
                        {
                            type = "ARRAY",
                            items = new
                            {
                                type = "OBJECT",
                                properties = new
                                {
                                    filePath = new { type = "STRING", description = "Sorunun bulunduğu dosya yolu" },
                                    lineNumber = new { type = "INTEGER", description = "Sorunun başladığı 1-tabanlı satır numarası (bulunamazsa 1 girin)" },
                                    lineContent = new { type = "STRING", description = "Sorunlu satırın içeriği" },
                                    severity = new { type = "STRING", @enum = new[] { "Info", "Warning", "Error", "Critical" }, description = "Önem derecesi" },
                                    category = new { type = "STRING", @enum = new[] { "Bug", "Security", "Performance", "SOLID", "CleanCode", "Refactoring", "CodeSmell", "Testing", "General" }, description = "Hata kategorisi" },
                                    title = new { type = "STRING", description = "Kısa ve açıklayıcı hata başlığı" },
                                    description = new { type = "STRING", description = "Sorunun ne olduğu, riskleri ve etkileri" },
                                    suggestion = new { type = "STRING", description = "Somut çözüm önerisi" },
                                    refactoredCode = new { type = "STRING", description = "Düzeltilmiş kod bloğu" }
                                },
                                required = new[] { "filePath", "lineNumber", "severity", "category", "title", "description", "suggestion" }
                            }
                        },
                        recommendations = new
                        {
                            type = "ARRAY",
                            description = "Tekil sorunların ötesinde genel iyileştirme önerileri",
                            items = new { type = "STRING" }
                        }
                    },
                    required = new[] { "detectedPurpose", "reliabilityScore", "verdict", "summary", "issues", "recommendations" }
                }
            }
        };

        var jsonPayload = JsonSerializer.Serialize(requestBody);
        var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        try
        {
            var text = await CallGeminiAsync(url, httpContent);

            var report = JsonSerializer.Deserialize<PullRequestReport>(text, DeserializeOptions);

            if (report == null)
            {
                throw new InvalidOperationException("Failed to deserialize pull request review JSON.");
            }

            report.RepoOwner = context.RepoOwner;
            report.RepoName = context.RepoName;
            report.PrNumber = context.PrNumber;
            report.PrTitle = context.PrTitle;
            report.PrUrl = context.PrUrl;
            report.Author = context.Author;
            report.BaseBranch = context.BaseBranch;
            report.HeadBranch = context.HeadBranch;
            report.HeadSha = context.HeadSha;
            report.CreatedAt = DateTime.UtcNow;

            return report;
        }
        catch (Exception ex)
        {
            return new PullRequestReport
            {
                RepoOwner = context.RepoOwner,
                RepoName = context.RepoName,
                PrNumber = context.PrNumber,
                PrTitle = context.PrTitle,
                PrUrl = context.PrUrl,
                Author = context.Author,
                BaseBranch = context.BaseBranch,
                HeadBranch = context.HeadBranch,
                ReliabilityScore = 0,
                Verdict = "Analiz Başarısız",
                Summary = $"Pull request incelemesi sırasında hata oluştu: {ex.Message}",
                Issues = new List<ReviewIssue>
                {
                    new ReviewIssue
                    {
                        FilePath = context.Files.FirstOrDefault()?.FileName ?? context.RepoName,
                        LineNumber = 1,
                        Severity = IssueSeverity.Critical,
                        Category = IssueCategory.General,
                        Title = "PR Analiz Hatası",
                        Description = $"Gemini servisine bağlanırken veya yanıtı ayrıştırırken hata oluştu. Hata detayı: {ex.Message}",
                        Suggestion = "API anahtarını, GitHub token'ını ve internet bağlantısını kontrol ediniz."
                    }
                }
            };
        }
    }

    private static readonly JsonSerializerOptions DeserializeOptions = CreateDeserializeOptions();

    private static JsonSerializerOptions CreateDeserializeOptions()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength] + "\n... (uzunluk sınırı nedeniyle kesildi)";
    }

    private async Task<string> CallGeminiAsync(string url, HttpContent httpContent)
    {
        var response = await _httpClient.PostAsync(url, httpContent);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(responseString);

        var text = document.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("API returns empty review result.");
        }

        return text;
    }
}
