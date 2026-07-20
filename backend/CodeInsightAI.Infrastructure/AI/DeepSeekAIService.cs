using System.Net.Http.Headers;
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

// Talks to DeepSeek's OpenAI-compatible chat completions API. Two models are useful here:
// "deepseek-chat" (DeepSeek-V3) for fast/cheap general review, and "deepseek-reasoner" (DeepSeek-R1)
// for deeper reasoning at higher latency/cost. Unlike Ollama and Gemini, DeepSeek does not support
// a strict JSON *schema*, only a JSON *object* mode - so the expected shape is described in the
// prompt as text and the response is parsed defensively (see ExtractJson) rather than being
// structurally guaranteed by the API.
public class DeepSeekAIService : IAIService
{
    private readonly HttpClient _httpClient;
    private readonly ISystemPromptRepository _systemPromptRepository;
    private readonly string _apiKey;
    private readonly string _model;

    public DeepSeekAIService(HttpClient httpClient, ISystemPromptRepository systemPromptRepository, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _systemPromptRepository = systemPromptRepository;

        _apiKey = configuration["DeepSeek:ApiKey"] ?? throw new InvalidOperationException("DeepSeek API key is not configured.");
        _model = configuration["DeepSeek:Model"] ?? "deepseek-chat";

        var baseUrl = configuration["DeepSeek:BaseUrl"];
        var normalizedBaseUrl = (string.IsNullOrWhiteSpace(baseUrl) ? "https://api.deepseek.com" : baseUrl).TrimEnd('/') + "/";
        _httpClient.BaseAddress = new Uri(normalizedBaseUrl);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
    }

    public async Task<PullRequestReport> AnalyzePullRequestAsync(PullRequestDiffContext context)
    {
        // Editable from the app's settings screen instead of being hardcoded - falls back to the
        // built-in default until the user customizes it.
        var systemPrompt = await _systemPromptRepository.GetCustomPromptAsync() ?? DefaultSystemPrompt.Text;

        // When the full raw diff came through, per-file patches would just be the same content
        // repeated a second time - wasteful at best, and at worst it crowds a large multi-file PR's
        // per-file patches + full contents out of the model's context window before the model ever
        // gets to the (more complete) raw diff section below. Only fall back to per-file patches when
        // there's no raw diff to rely on.
        var hasRawDiff = !string.IsNullOrWhiteSpace(context.RawDiff);

        var filesSection = new StringBuilder();
        foreach (var file in context.Files)
        {
            filesSection.AppendLine($"### Dosya: {file.FileName}");

            if (!hasRawDiff)
            {
                filesSection.AppendLine("Diff (patch):");
                filesSection.AppendLine("```diff");
                filesSection.AppendLine(Truncate(file.Patch, 8000));
                filesSection.AppendLine("```");
            }

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
{Truncate(context.RawDiff, 200000)}
```

";

        var promptText = $@"Repo: {context.RepoOwner}/{context.RepoName}
PR #{context.PrNumber}: {context.PrTitle}
Yazan: {context.Author}
Branch: {context.HeadBranch} -> {context.BaseBranch}

PR Açıklaması:
{(string.IsNullOrWhiteSpace(context.PrDescription) ? "(açıklama girilmemiş)" : context.PrDescription)}

{rawDiffSection}Değişen dosyalar ({context.Files.Count} adet):

{filesSection}
{JsonShapeInstruction}";

        var requestBody = new
        {
            model = _model,
            stream = false,
            max_tokens = 8192,
            // DeepSeek only supports a plain JSON-object mode (not a strict schema); the exact shape
            // is enforced via the JsonShapeInstruction text appended to the prompt above.
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = promptText },
            },
        };

        var jsonPayload = JsonSerializer.Serialize(requestBody);
        var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        try
        {
            var text = await CallDeepSeekAsync(httpContent);

            var report = JsonSerializer.Deserialize<PullRequestReport>(ExtractJson(text), DeserializeOptions);

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
                        Description = $"DeepSeek servisine bağlanırken veya yanıtı ayrıştırırken hata oluştu. Hata detayı: {ex.Message}",
                        Suggestion = "DeepSeek API anahtarını, model adını (deepseek-chat / deepseek-reasoner) ve internet bağlantısını kontrol ediniz."
                    }
                }
            };
        }
    }

    // DeepSeek can't enforce a JSON schema server-side, so the shape it must produce is spelled out
    // here in the prompt. Field names match PullRequestReport/ReviewIssue exactly so the same
    // camelCase deserializer used for the other providers works unchanged.
    private const string JsonShapeInstruction = @"Analiz sonucunu SADECE aşağıdaki yapıya birebir uyan tek bir JSON nesnesi olarak döndür (başka hiçbir metin, açıklama veya markdown ekleme):
{
  ""detectedPurpose"": ""string - PR'ın amacını anlatan en az 1-2 uzun paragraf"",
  ""reliabilityScore"": 0-100 arası tam sayı,
  ""verdict"": ""string - bulguları özetleyen kısa etiket (onay/red kararı DEĞİL)"",
  ""summary"": ""string - genel değerlendirme, riskler ve tutarlılık"",
  ""issues"": [
    {
      ""filePath"": ""string - sorunun bulunduğu dosya yolu"",
      ""lineNumber"": 1-tabanlı tam sayı (bulunamazsa 1),
      ""lineContent"": ""string - sorunlu satırı/kod bloğunu diff'ten birebir kopyala"",
      ""severity"": ""Info | Warning | Error | Critical"",
      ""category"": ""Bug | Security | Performance | SOLID | CleanCode | Refactoring | CodeSmell | Testing | General"",
      ""title"": ""string - kısa ve açıklayıcı başlık"",
      ""description"": ""string - sorunun ne olduğu, riskleri ve etkileri"",
      ""suggestion"": ""string - somut çözüm önerisi"",
      ""refactoredCode"": ""string - düzeltilmiş kod bloğu (yoksa boş string)""
    }
  ],
  ""recommendations"": [""string - genel iyileştirme önerileri""]
}";

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

    // deepseek-reasoner in particular can wrap its answer in ```json fences or prepend stray text
    // despite json_object mode, so pull out the outermost { ... } block before deserializing.
    private static string ExtractJson(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            return text[start..(end + 1)];
        }

        return text;
    }

    private async Task<string> CallDeepSeekAsync(HttpContent httpContent)
    {
        var response = await _httpClient.PostAsync("chat/completions", httpContent);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(responseString);

        var text = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("DeepSeek returned an empty review result.");
        }

        return text;
    }
}
