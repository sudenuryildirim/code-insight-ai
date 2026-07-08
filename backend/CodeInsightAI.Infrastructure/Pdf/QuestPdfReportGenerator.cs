using CodeInsightAI.Application.Interfaces;
using CodeInsightAI.Domain.Entities;
using CodeInsightAI.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CodeInsightAI.Infrastructure.Pdf;

/// <summary>
/// Renders a CodeReviewReport as a clean, light-themed PDF using QuestPDF.
/// </summary>
public class QuestPdfReportGenerator : IPdfReportGenerator
{
    private const string TextPrimary = "#111827";
    private const string TextSecondary = "#374151";
    private const string TextMuted = "#6B7280";
    private const string BorderColor = "#E5E7EB";
    private const string SurfaceColor = "#F9FAFB";
    private const string CodeBg = "#111827";
    private const string CodeFg = "#F9FAFB";

    static QuestPdfReportGenerator()
    {
        // Community license is free for small teams / individuals (QuestPDF requirement).
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Generate(CodeReviewReport report)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(32);
                page.PageColor("#FFFFFF");
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(TextSecondary));

                page.Header().Element(c => ComposeHeader(c, report));
                page.Content().Element(c => ComposeContent(c, report));
                page.Footer().Element(ComposeFooter);
            });
        });

        return document.GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, CodeReviewReport report)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Kod İnceleme Raporu").FontSize(20).Bold().FontColor(TextPrimary);

                    col.Item().PaddingTop(2).Text(
                        string.IsNullOrWhiteSpace(report.FileName) ? "Dosya adı belirtilmedi" : report.FileName)
                        .FontSize(11).FontColor(TextMuted);

                    col.Item().PaddingTop(6).Row(r =>
                    {
                        r.AutoItem().Element(e => Chip(e,
                            string.IsNullOrWhiteSpace(report.DetectedLanguage) ? "Dil tespit edilemedi" : report.DetectedLanguage,
                            "#EFF6FF", "#1D4ED8"));
                        r.ConstantItem(8);
                        r.AutoItem().AlignMiddle().Text($"Oluşturulma: {report.CreatedAt:dd.MM.yyyy HH:mm}")
                            .FontSize(9).FontColor(TextMuted);
                    });
                });

                row.ConstantItem(84).Element(e => ScoreBadge(e, report.Score));
            });

            column.Item().PaddingTop(12).LineHorizontal(1).LineColor(BorderColor);
        });
    }

    private static void ScoreBadge(IContainer container, int score)
    {
        var (bg, fg) = ScoreColors(score);

        container
            .Background(bg)
            .CornerRadius(8)
            .Padding(10)
            .Column(col =>
            {
                col.Item().AlignCenter().Text(score.ToString()).FontSize(22).Bold().FontColor(fg);
                col.Item().AlignCenter().Text("/ 100").FontSize(8).FontColor(fg);
            });
    }

    private static void Chip(IContainer container, string text, string bg, string fg)
    {
        container.Background(bg).CornerRadius(4).Padding(4)
            .Text(text).FontSize(9).Bold().FontColor(fg);
    }

    private static void ComposeContent(IContainer container, CodeReviewReport report)
    {
        container.PaddingTop(12).Column(column =>
        {
            column.Spacing(16);

            column.Item().Element(c => Section(c, "Kodun Amacı", report.CodePurpose));
            column.Item().Element(c => Section(c, "Genel Değerlendirme", report.Summary));

            if (report.Strengths.Count > 0)
            {
                column.Item().Element(c => BulletSection(c, "Güçlü Yönler", report.Strengths, "#16A34A"));
            }

            column.Item().Element(c => IssuesSection(c, report.Issues));

            if (report.Recommendations.Count > 0)
            {
                column.Item().Element(c => BulletSection(c, "Genel Öneriler", report.Recommendations, "#2563EB"));
            }
        });
    }

    private static void Section(IContainer container, string title, string body)
    {
        container.Column(col =>
        {
            col.Item().Text(title).FontSize(13).Bold().FontColor(TextPrimary);
            col.Item().PaddingTop(4).Text(string.IsNullOrWhiteSpace(body) ? "-" : body)
                .FontSize(10).FontColor(TextSecondary).LineHeight(1.35f);
        });
    }

    private static void BulletSection(IContainer container, string title, List<string> items, string accent)
    {
        container.Column(col =>
        {
            col.Item().Text(title).FontSize(13).Bold().FontColor(TextPrimary);
            col.Item().PaddingTop(6).Column(inner =>
            {
                inner.Spacing(4);
                foreach (var item in items)
                {
                    inner.Item().Row(row =>
                    {
                        row.ConstantItem(12).Text("•").FontColor(accent).Bold();
                        row.RelativeItem().Text(item).FontSize(10).FontColor(TextSecondary).LineHeight(1.3f);
                    });
                }
            });
        });
    }

    private static void IssuesSection(IContainer container, List<ReviewIssue> issues)
    {
        container.Column(col =>
        {
            col.Item().Text($"Tespit Edilen Sorunlar ({issues.Count})").FontSize(13).Bold().FontColor(TextPrimary);

            if (issues.Count == 0)
            {
                col.Item().PaddingTop(4).Text("Analiz sırasında herhangi bir sorun tespit edilmedi.")
                    .FontSize(10).FontColor(TextSecondary);
                return;
            }

            var ordered = issues.OrderBy(i => SeverityRank(i.Severity)).ThenBy(i => i.LineNumber).ToList();

            col.Item().PaddingTop(8).Column(inner =>
            {
                inner.Spacing(10);
                foreach (var issue in ordered)
                {
                    inner.Item().Element(c => IssueCard(c, issue));
                }
            });
        });
    }

    private static void IssueCard(IContainer container, ReviewIssue issue)
    {
        var (badgeBg, badgeFg) = SeverityColors(issue.Severity);

        container
            .Background(SurfaceColor)
            .Border(1)
            .BorderColor(BorderColor)
            .CornerRadius(6)
            .Padding(10)
            .Column(col =>
            {
                col.Spacing(6);

                col.Item().Row(row =>
                {
                    row.AutoItem().Element(e => Chip(e, SeverityLabel(issue.Severity), badgeBg, badgeFg));
                    row.ConstantItem(6);
                    row.AutoItem().Element(e => Chip(e, CategoryLabel(issue.Category), "#F3F4F6", TextSecondary));
                    row.RelativeItem();
                    row.AutoItem().AlignMiddle().Text($"{issue.FilePath}:{issue.LineNumber}")
                        .FontSize(9).FontColor(TextMuted);
                });

                col.Item().Text(issue.Title).FontSize(11).Bold().FontColor(TextPrimary);

                if (!string.IsNullOrWhiteSpace(issue.LineContent))
                {
                    col.Item().Background(CodeBg).CornerRadius(4).Padding(6)
                        .Text(issue.LineContent).FontFamily("Consolas").FontSize(9).FontColor(CodeFg);
                }

                col.Item().Text(issue.Description).FontSize(10).FontColor(TextSecondary).LineHeight(1.3f);

                if (!string.IsNullOrWhiteSpace(issue.Suggestion))
                {
                    col.Item().Background("#EFF6FF").CornerRadius(4).Padding(8).Column(sc =>
                    {
                        sc.Item().Text("Öneri").FontSize(9).Bold().FontColor("#1D4ED8");
                        sc.Item().PaddingTop(2).Text(issue.Suggestion).FontSize(10).FontColor("#1E3A8A").LineHeight(1.3f);
                    });
                }

                if (!string.IsNullOrWhiteSpace(issue.RefactoredCode))
                {
                    col.Item().Background(CodeBg).CornerRadius(4).Padding(8).Column(rc =>
                    {
                        rc.Item().Text("Önerilen Kod").FontSize(9).Bold().FontColor("#9CA3AF");
                        rc.Item().PaddingTop(4).Text(issue.RefactoredCode).FontFamily("Consolas").FontSize(9).FontColor(CodeFg);
                    });
                }
            });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(1).LineColor(BorderColor);
            col.Item().PaddingTop(6).Row(row =>
            {
                row.RelativeItem().Text("CodeInsightAI tarafından otomatik olarak oluşturulmuştur.")
                    .FontSize(8).FontColor(TextMuted);

                row.AutoItem().Text(text =>
                {
                    text.DefaultTextStyle(x => x.FontSize(8).FontColor(TextMuted));
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        });
    }

    private static int SeverityRank(IssueSeverity severity) => severity switch
    {
        IssueSeverity.Critical => 0,
        IssueSeverity.Error => 1,
        IssueSeverity.Warning => 2,
        _ => 3
    };

    private static string SeverityLabel(IssueSeverity severity) => severity switch
    {
        IssueSeverity.Critical => "KRİTİK",
        IssueSeverity.Error => "HATA",
        IssueSeverity.Warning => "UYARI",
        _ => "BİLGİ"
    };

    private static (string bg, string fg) SeverityColors(IssueSeverity severity) => severity switch
    {
        IssueSeverity.Critical => ("#FEE2E2", "#B91C1C"),
        IssueSeverity.Error => ("#FFEDD5", "#C2410C"),
        IssueSeverity.Warning => ("#FEF3C7", "#B45309"),
        _ => ("#DBEAFE", "#1D4ED8")
    };

    private static (string bg, string fg) ScoreColors(int score)
    {
        if (score >= 80) return ("#DCFCE7", "#15803D");
        if (score >= 50) return ("#FEF3C7", "#B45309");
        return ("#FEE2E2", "#B91C1C");
    }

    private static string CategoryLabel(IssueCategory category) => category switch
    {
        IssueCategory.Bug => "Hata (Bug)",
        IssueCategory.Security => "Güvenlik",
        IssueCategory.Performance => "Performans",
        IssueCategory.SOLID => "SOLID",
        IssueCategory.CleanCode => "Temiz Kod",
        IssueCategory.Refactoring => "Refactoring",
        IssueCategory.CodeSmell => "Code Smell",
        IssueCategory.Testing => "Test",
        _ => "Genel"
    };
}
