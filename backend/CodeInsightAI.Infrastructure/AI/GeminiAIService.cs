using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeInsightAI.Application.DTOs;
using CodeInsightAI.Application.Interfaces;
using CodeInsightAI.Domain.Entities;
using CodeInsightAI.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace CodeInsightAI.Infrastructure.AI;

public class GeminiAIService : IAIService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;

    public GeminiAIService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiKey = configuration["Gemini:ApiKey"] ?? throw new InvalidOperationException("Gemini API key is not configured.");
        _model = configuration["Gemini:Model"] ?? "gemini-2.5-flash";
    }

    public async Task<PullRequestReport> AnalyzePullRequestAsync(PullRequestDiffContext context)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";

        var systemPrompt = @"Sen bir yazılım ekibinde pull request'leri gözden geçiren, kıdemli bir kod reviewer ve güvenlik uzmanısın.
Görevin, sana verilen pull request'in diff'ini (ve mümkünse ilgili dosyaların PR sonrası tam içeriğini) inceleyip aşağıdaki soruları SON DERECE DETAYLI ve AÇIKLAYICI şekilde yanıtlamaktır:

1. Bu PR'ın amacı ne? Başlık, açıklama ve diff'e bakarak PR'ın hangi problemi çözmeye veya hangi özelliği eklemeye çalıştığını en az 1-2 uzun paragrafla açıkla.
2. Değişiklikte bug, mantık hatası veya çalışma zamanı hatası olasılığı var mı?
3. OWASP Top 10 standartlarına göre güvenlik açığı (SQL Injection, XSS, hardcoded secret/api key, eksik doğrulama, zayıf algoritma vb.) var mı?
4. Değişiklik, dosyanın (ve varsa projenin) geri kalanıyla TUTARLI mı? Mevcut isimlendirme, mimari desenler, hata yönetimi biçimiyle çelişen bir şey var mı? (Bunun için sana dosyaların PR sonrası tam içeriği de verildiyse onu bağlam olarak kullan.)
5. Performans problemi, gereksiz döngü veya kötü kod kokusu var mı?

Kurallar:
- SEN ONAY (approve) YA DA MERGE KARARI VERMEZSİN. Sadece bulgularını raporlarsın; nihai karar her zaman bir insana aittir. 'verdict' alanına sadece bulgularını özetleyen kısa bir etiket yaz (örn. 'Onaya Hazır Görünüyor', 'Küçük Düzeltmeler Önerilir', 'Değişiklik Gerekli', 'Riskli - Dikkatli İncelenmeli').
- reliabilityScore, PR'ın genel güvenilirliğini 0-100 arası bir sayı ile ifade eder (bug/güvenlik/tutarlılık sorunları düştükçe skor düşer).
- Her tespit edilen sorun için: sorunun NEDEN bir sorun olduğunu, risklerini ve somut bir ÇÖZÜM ÖNERİSİni detaylı yaz; mümkünse refactoredCode ver.
- detectedPurpose ve summary alanlarını asla kısa geçme.
- Yanıt dilin her zaman TÜRKÇE olmalıdır (kod içindeki teknik terimler İngilizce kalabilir).
- Analiz sonucunu JSON formatında döndür.";

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

        var promptText = $@"Repo: {context.RepoOwner}/{context.RepoName}
PR #{context.PrNumber}: {context.PrTitle}
Yazan: {context.Author}
Branch: {context.HeadBranch} -> {context.BaseBranch}

PR Açıklaması:
{(string.IsNullOrWhiteSpace(context.PrDescription) ? "(açıklama girilmemiş)" : context.PrDescription)}

Değişen dosyalar ({context.Files.Count} adet):

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
