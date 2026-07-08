using CodeInsightAI.Application.Interfaces;
using CodeInsightAI.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace CodeInsightAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PullRequestController : ControllerBase
{
    private readonly IPullRequestReviewService _pullRequestReviewService;
    private readonly IPdfReportGenerator _pdfReportGenerator;

    public PullRequestController(IPullRequestReviewService pullRequestReviewService, IPdfReportGenerator pdfReportGenerator)
    {
        _pullRequestReviewService = pullRequestReviewService;
        _pdfReportGenerator = pdfReportGenerator;
    }

    // Lists the currently open pull requests for the configured repo (GitHub:Owner/GitHub:Repo).
    [HttpGet("open")]
    public async Task<IActionResult> GetOpenPullRequests()
    {
        try
        {
            var pullRequests = await _pullRequestReviewService.GetOpenPullRequestsAsync();
            return Ok(pullRequests);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Açık pull request'ler alınırken sunucu hatası oluştu.", error = ex.Message });
        }
    }

    // Analyzes a single pull request's diff and returns a review report.
    // This never approves or merges anything on GitHub - the merge decision stays with a human.
    [HttpPost("{number:int}/review")]
    public async Task<IActionResult> ReviewPullRequest(int number)
    {
        try
        {
            var report = await _pullRequestReviewService.ReviewPullRequestAsync(number);
            return Ok(report);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Pull request incelenirken sunucu hatası oluştu.", error = ex.Message });
        }
    }

    // Renders an already-generated report (as returned by /{number}/review) into a PDF file.
    // Kept separate so downloading the PDF never triggers a second AI call.
    [HttpPost("report/pdf")]
    public IActionResult GeneratePdf([FromBody] CodeReviewReport report)
    {
        try
        {
            var pdfBytes = _pdfReportGenerator.Generate(report);
            var safeFileName = string.IsNullOrWhiteSpace(report.FileName) ? "rapor" : report.FileName;
            var downloadName = $"pr-inceleme-raporu-{safeFileName}.pdf".Replace(' ', '-');
            return File(pdfBytes, "application/pdf", downloadName);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "PDF oluşturulurken sunucu hatası oluştu.", error = ex.Message });
        }
    }
}
