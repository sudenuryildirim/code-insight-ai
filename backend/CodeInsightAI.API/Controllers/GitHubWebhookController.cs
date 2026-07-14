using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeInsightAI.API.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace CodeInsightAI.API.Controllers;

[ApiController]
[Route("api/github")]
public class GitHubWebhookController : ControllerBase
{
    private static readonly string[] RelevantActions = { "opened", "synchronize", "reopened" };

    private readonly IConfiguration _configuration;
    private readonly ILogger<GitHubWebhookController> _logger;
    private readonly IHubContext<PullRequestHub> _hubContext;

    public GitHubWebhookController(IConfiguration configuration, ILogger<GitHubWebhookController> logger, IHubContext<PullRequestHub> hubContext)
    {
        _configuration = configuration;
        _logger = logger;
        _hubContext = hubContext;
    }

    // Receives GitHub's pull_request webhook events. Signature-verified so only GitHub (or someone
    // holding the configured secret) can call this. Doesn't take any action on GitHub itself - it
    // only pushes a "the PR list changed" notification to connected clients over SignalR so the UI
    // can refresh itself without a manual "Yenile" click. The PR list is always re-fetched live from
    // GitHub when that happens, so a missed or delayed notification never leaves stale data behind.
    [HttpPost("webhook")]
    public async Task<IActionResult> ReceiveWebhook()
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync();
        Request.Body.Position = 0;

        var secret = _configuration["GitHub:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            _logger.LogWarning("GitHub webhook secret is not configured; rejecting webhook call.");
            return StatusCode(500, new { message = "Webhook secret sunucuda yapılandırılmamış." });
        }

        if (!IsSignatureValid(rawBody, Request.Headers["X-Hub-Signature-256"], secret))
        {
            return Unauthorized(new { message = "Geçersiz webhook imzası." });
        }

        var eventType = Request.Headers["X-GitHub-Event"].ToString();
        if (eventType != "pull_request")
        {
            return Ok(new { received = true, ignored = true });
        }

        using var payload = JsonDocument.Parse(rawBody);
        var action = payload.RootElement.TryGetProperty("action", out var actionProp) ? actionProp.GetString() : null;
        var prNumber = payload.RootElement.TryGetProperty("number", out var numberProp) ? numberProp.GetInt32() : (int?)null;

        string? owner = null;
        string? repo = null;
        if (payload.RootElement.TryGetProperty("repository", out var repoProp))
        {
            repo = repoProp.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
            if (repoProp.TryGetProperty("owner", out var ownerProp) && ownerProp.TryGetProperty("login", out var loginProp))
            {
                owner = loginProp.GetString();
            }
        }

        if (action != null && RelevantActions.Contains(action))
        {
            _logger.LogInformation("GitHub webhook: {Owner}/{Repo} PR #{PrNumber} {Action}", owner, repo, prNumber, action);
            await _hubContext.Clients.All.SendAsync("pullRequestChanged", new { owner, repo, prNumber, action });
        }

        return Ok(new { received = true });
    }

    // Internal (rather than private) so it can be unit-tested directly without spinning up
    // a full HTTP pipeline just to exercise the HMAC comparison logic.
    internal static bool IsSignatureValid(string rawBody, string? signatureHeader, string secret)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader) || !signatureHeader.StartsWith("sha256="))
        {
            return false;
        }

        var expectedHex = signatureHeader["sha256=".Length..];

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computedBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        var computedHex = Convert.ToHexString(computedBytes).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHex),
            Encoding.UTF8.GetBytes(expectedHex));
    }
}
