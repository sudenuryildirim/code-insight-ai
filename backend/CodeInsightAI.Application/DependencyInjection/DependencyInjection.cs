using CodeInsightAI.Application.Interfaces;
using CodeInsightAI.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CodeInsightAI.Application.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ICodeReviewService, CodeReviewService>();
        services.AddScoped<IPullRequestReviewService, PullRequestReviewService>();
        return services;
    }
}
