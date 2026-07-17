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

// Talks to a local Ollama server instead of a cloud AI API, so PR diffs and file contents never
// leave the company's own infrastructure. Requires the configured model (default gpt-oss:20b) to
// already be pulled on the machine hosting Ollama: `ollama pull gpt-oss:20b`.
public class OllamaAIService : IAIService
{
    private readonly HttpClient _httpClient;
    private readonly ISystemPromptRepository _systemPromptRepository;
    private readonly string _model;

    public OllamaAIService(HttpClient httpClient, ISystemPromptRepository systemPromptRepository, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _systemPromptRepository = systemPromptRepository;

        var baseUrl = configuration["Ollama:BaseUrl"];
        var normalizedBaseUrl = (string.IsNullOrWhiteSpace(baseUrl) ? "http://localhost:11434" : baseUrl).TrimEnd('/') + "/";
        _httpClient.BaseAddress = new Uri(normalizedBaseUrl);

        _model = configuration["Ollama:Model"] ?? "gpt-oss:20b";
    }

    public async Task<PullRequestReport> AnalyzePullRequestAsync(PullRequestDiffContext context)
    {
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
            model = _model,
            stream = false,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = promptText },
            },
            // Ollama's structured-output support: constrains the model's response to this JSON Schema.
            format = new
            {
                type = "object",
                properties = new
                {
                    detectedPurpose = new { type = "string", description = "PR'ın ne yapmaya çalıştığını anlatan, en az 1-2 uzun paragraftan oluşan detaylı Türkçe açıklama" },
                    reliabilityScore = new { type = "integer", description = "PR'ın genel güvenilirlik skoru (0-100 arası)" },
                    verdict = new { type = "string", description = "Bulguları özetleyen kısa etiket, örn. 'Onaya Hazır Görünüyor', 'Değişiklik Gerekli'. Bu bir onay/red kararı DEĞİLDİR, sadece özet." },
                    summary = new { type = "string", description = "PR'ın genel değerlendirmesini, risklerini ve tutarlılığını özetleyen uzun Türkçe değerlendirme" },
                    issues = new
                    {
                        type = "array",
                        items = new
                        {
                            type = "object",
                            properties = new
                            {
                                filePath = new { type = "string", description = "Sorunun bulunduğu dosya yolu" },
                                lineNumber = new { type = "integer", description = "Sorunun başladığı 1-tabanlı satır numarası (bulunamazsa 1 girin)" },
                                lineContent = new { type = "string", description = "Sorunlu satırın içeriği" },
                                severity = new { type = "string", @enum = new[] { "Info", "Warning", "Error", "Critical" }, description = "Önem derecesi" },
                                category = new { type = "string", @enum = new[] { "Bug", "Security", "Performance", "SOLID", "CleanCode", "Refactoring", "CodeSmell", "Testing", "General" }, description = "Hata kategorisi" },
                                title = new { type = "string", description = "Kısa ve açıklayıcı hata başlığı" },
                                description = new { type = "string", description = "Sorunun ne olduğu, riskleri ve etkileri" },
                                suggestion = new { type = "string", description = "Somut çözüm önerisi" },
                                refactoredCode = new { type = "string", description = "Düzeltilmiş kod bloğu" }
                            },
                            required = new[] { "filePath", "lineNumber", "severity", "category", "title", "description", "suggestion" }
                        }
                    },
                    recommendations = new
                    {
                        type = "array",
                        description = "Tekil sorunların ötesinde genel iyileştirme önerileri",
                        items = new { type = "string" }
                    }
                },
                required = new[] { "detectedPurpose", "reliabilityScore", "verdict", "summary", "issues", "recommendations" }
            }
        };

        var jsonPayload = JsonSerializer.Serialize(requestBody);
        var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        try
        {
            var text = await CallOllamaAsync(httpContent);

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
                        Description = $"Ollama servisine bağlanırken veya yanıtı ayrıştırırken hata oluştu. Hata detayı: {ex.Message}",
                        Suggestion = "Ollama'nın çalıştığını (ollama serve), yapılandırılan modelin pull edildiğini (ollama pull gpt-oss:20b) ve Ollama:BaseUrl ayarını kontrol ediniz."
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

    private async Task<string> CallOllamaAsync(HttpContent httpContent)
    {
        var response = await _httpClient.PostAsync("api/chat", httpContent);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(responseString);

        var text = document.RootElement
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Ollama returned an empty review result.");
        }

        return text;
    }
}
