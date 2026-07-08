using CodeInsightAI.Application.DTOs;
using CodeInsightAI.Application.Interfaces;
using CodeInsightAI.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace CodeInsightAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CodeReviewController : ControllerBase
{
    private readonly ICodeReviewService _codeReviewService;
    private readonly IPdfReportGenerator _pdfReportGenerator;

    public CodeReviewController(ICodeReviewService codeReviewService, IPdfReportGenerator pdfReportGenerator)
    {
        _codeReviewService = codeReviewService;
        _pdfReportGenerator = pdfReportGenerator;
    }

    // Analyzes the submitted code and returns the review report as JSON.
    // The caller never picks a programming language - the AI detects it.
    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze([FromBody] ReviewRequestDto request)
    {
        try
        {
            var report = await _codeReviewService.AnalyzeCodeAsync(request);
            return Ok(report);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Kod analizi sırasında sunucu hatası oluştu.", error = ex.Message });
        }
    }

    // Renders an already-generated report (as returned by /analyze) into a PDF file.
    // Kept separate from /analyze so downloading the PDF never triggers a second AI call.
    [HttpPost("report/pdf")]
    public IActionResult GeneratePdf([FromBody] CodeReviewReport report)
    {
        try
        {
            var pdfBytes = _pdfReportGenerator.Generate(report);
            var safeFileName = string.IsNullOrWhiteSpace(report.FileName) ? "rapor" : report.FileName;
            var downloadName = $"kod-inceleme-raporu-{safeFileName}.pdf".Replace(' ', '-');
            return File(pdfBytes, "application/pdf", downloadName);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "PDF oluşturulurken sunucu hatası oluştu.", error = ex.Message });
        }
    }
}
