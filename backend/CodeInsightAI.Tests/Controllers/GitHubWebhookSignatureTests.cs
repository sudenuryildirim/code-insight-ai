using System.Security.Cryptography;
using System.Text;
using CodeInsightAI.API.Controllers;

namespace CodeInsightAI.Tests.Controllers;

public class GitHubWebhookSignatureTests
{
    private const string Secret = "test-secret-123";
    private const string Body = "{\"action\":\"opened\",\"number\":42}";

    private static string ComputeSignature(string body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    [Fact]
    public void IsSignatureValid_accepts_a_correctly_signed_payload()
    {
        var signature = ComputeSignature(Body, Secret);

        Assert.True(GitHubWebhookController.IsSignatureValid(Body, signature, Secret));
    }

    [Fact]
    public void IsSignatureValid_rejects_a_signature_computed_with_the_wrong_secret()
    {
        var signature = ComputeSignature(Body, "a-different-secret");

        Assert.False(GitHubWebhookController.IsSignatureValid(Body, signature, Secret));
    }

    [Fact]
    public void IsSignatureValid_rejects_a_signature_for_a_different_body()
    {
        var signature = ComputeSignature("{\"action\":\"closed\",\"number\":42}", Secret);

        Assert.False(GitHubWebhookController.IsSignatureValid(Body, signature, Secret));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-even-close-to-a-signature")]
    [InlineData("sha1=deadbeef")] // right prefix scheme is sha256, not sha1
    public void IsSignatureValid_rejects_malformed_or_missing_header(string? header)
    {
        Assert.False(GitHubWebhookController.IsSignatureValid(Body, header, Secret));
    }
}
