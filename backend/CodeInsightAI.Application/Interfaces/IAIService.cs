using CodeInsightAI.Application.DTOs;
using CodeInsightAI.Domain.Entities;

namespace CodeInsightAI.Application.Interfaces;

public interface IAIService
{
    // customInstruction, when provided, is added on top of the standard review checklist (bug/
    // security/consistency/etc.) rather than replacing it, so the structured report shape the UI
    // depends on is always produced.
    Task<PullRequestReport> AnalyzePullRequestAsync(PullRequestDiffContext context, string? customInstruction = null);
}
