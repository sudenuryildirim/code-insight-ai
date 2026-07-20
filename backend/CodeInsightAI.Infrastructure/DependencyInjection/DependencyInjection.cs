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
        // All AI providers are registered so the app never fails to start regardless of which one
        // AI:Provider selects; only the chosen implementation is actually constructed at request time,
        // so an unset Gemini:ApiKey is harmless when Ollama is selected (and vice versa).
        services.AddHttpClient<GeminiAIService>();
        // deepseek-reasoner (R1) in particular can take a while on a large diff - give it headroom.
        services.AddHttpClient<DeepSeekAIService>(client => client.Timeout = TimeSpan.FromMinutes(5));
        // Local model inference can take much longer than a cloud API call, especially for a 20B
        // model on modest hardware with a large PR diff in context - give it room to finish.
        services.AddHttpClient<OllamaAIService>(client => client.Timeout = TimeSpan.FromMinutes(10));
        services.AddScoped<IAIService>(sp =>
        {
            var provider = configuration["AI:Provider"];
            if (string.Equals(provider, "Gemini", StringComparison.OrdinalIgnoreCase))
            {
                return sp.GetRequiredService<GeminiAIService>();
            }
            if (string.Equals(provider, "DeepSeek", StringComparison.OrdinalIgnoreCase))
            {
                return sp.GetRequiredService<DeepSeekAIService>();
            }
            return sp.GetRequiredService<OllamaAIService>();
        });
        services.AddHttpClient<IGitHubService, GitHubService>();
        services.AddSingleton<IPdfReportGenerator, QuestPdfReportGenerator>();

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("Default")));
        services.AddScoped<IPullRequestReviewRepository, PullRequestReviewRepository>();
        services.AddScoped<ISystemPromptRepository, SystemPromptRepository>();

        return services;
    }
}
