namespace CodeInsightAI.Application.Interfaces;

public interface ISystemPromptRepository
{
    // Null when the user hasn't customized it yet - callers fall back to DefaultSystemPrompt.Text.
    Task<string?> GetCustomPromptAsync();

    Task SaveCustomPromptAsync(string content);

    Task ResetToDefaultAsync();
}
