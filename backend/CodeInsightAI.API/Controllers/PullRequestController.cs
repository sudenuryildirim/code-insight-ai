using CodeInsightAI.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CodeInsightAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PullRequestController : ControllerBase
{
    private readonly IPullRequestReviewService _pullRequestReviewService;

    public PullRequestController(IPullRequestReviewService pullRequestReviewService)
    {
        _pullRequestReviewService = pullRequestReviewService;
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
}
