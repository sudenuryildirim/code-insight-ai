using CodeInsightAI.Application.Interfaces;
using CodeInsightAI.Infrastructure.AI;
using CodeInsightAI.Infrastructure.GitHub;
using CodeInsightAI.Infrastructure.Pdf;
using CodeInsightAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CodeInsightAI.Infrastructure.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<IAIService, GeminiAIService>();
        services.AddHttpClient<IGitHubService, GitHubService>();
        services.AddSingleton<IPdfReportGenerator, QuestPdfReportGenerator>();

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("Default")));
        services.AddScoped<IPullRequestReviewRepository, PullRequestReviewRepository>();

        return services;
    }
}
