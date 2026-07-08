using CodeInsightAI.Application.Interfaces;
using CodeInsightAI.Infrastructure.AI;
using CodeInsightAI.Infrastructure.GitHub;
using CodeInsightAI.Infrastructure.Pdf;
using Microsoft.Extensions.DependencyInjection;

namespace CodeInsightAI.Infrastructure.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddHttpClient<IAIService, GeminiAIService>();
        services.AddHttpClient<IGitHubService, GitHubService>();
        services.AddSingleton<IPdfReportGenerator, QuestPdfReportGenerator>();
        return services;
    }
}
