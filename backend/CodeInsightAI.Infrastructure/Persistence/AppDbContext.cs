using System.Text.Json;
using CodeInsightAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace CodeInsightAI.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<PullRequestReport> PullRequestReports => Set<PullRequestReport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PullRequestReport>(entity =>
        {
            entity.HasKey(r => r.Id);

            entity.Property(r => r.Recommendations)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
                .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                    (a, b) => (a ?? new List<string>()).SequenceEqual(b ?? new List<string>()),
                    v => v.Aggregate(0, (hash, s) => HashCode.Combine(hash, s)),
                    v => v.ToList()));

            entity.OwnsMany(r => r.Issues, issues =>
            {
                issues.WithOwner().HasForeignKey("PullRequestReportId");
                issues.Property<int>("Id");
                issues.HasKey("Id");
                issues.ToTable("PullRequestReportIssues");
            });

            // Lets the review-history and cache-lookup queries filter by repo + PR without a table scan.
            entity.HasIndex(r => new { r.RepoOwner, r.RepoName, r.PrNumber });
        });
    }
}
