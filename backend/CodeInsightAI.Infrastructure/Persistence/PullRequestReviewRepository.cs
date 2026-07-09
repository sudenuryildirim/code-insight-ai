using CodeInsightAI.Application.Interfaces;
using CodeInsightAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeInsightAI.Infrastructure.Persistence;

public class PullRequestReviewRepository : IPullRequestReviewRepository
{
    private readonly AppDbContext _dbContext;

    public PullRequestReviewRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PullRequestReport?> GetLatestReviewAsync(string owner, string repo, int prNumber, string headSha)
    {
        return await _dbContext.PullRequestReports
            .Include(r => r.Issues)
            .Where(r => r.RepoOwner == owner && r.RepoName == repo && r.PrNumber == prNumber && r.HeadSha == headSha)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<List<PullRequestReport>> GetReviewHistoryAsync(string owner, string repo, int prNumber)
    {
        return await _dbContext.PullRequestReports
            .Include(r => r.Issues)
            .Where(r => r.RepoOwner == owner && r.RepoName == repo && r.PrNumber == prNumber)
            .OrderByDescending(r => r.CreatedAt)
            .Take(10)
            .ToListAsync();
    }

    public async Task SaveReviewAsync(PullRequestReport report)
    {
        _dbContext.PullRequestReports.Add(report);
        await _dbContext.SaveChangesAsync();
    }
}
