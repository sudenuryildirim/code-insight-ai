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
        // Local model inference can take much longer than a cloud API call, especially for a 20B
        // model on modest hardware with a large PR diff in context - give it room to finish.
        services.AddHttpClient<IAIService, OllamaAIService>(client => client.Timeout = TimeSpan.FromMinutes(10));
        services.AddHttpClient<IGitHubService, GitHubService>();
        services.AddSingleton<IPdfReportGenerator, QuestPdfReportGenerator>();

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("Default")));
        services.AddScoped<IPullRequestReviewRepository, PullRequestReviewRepository>();

        return services;
    }
}
