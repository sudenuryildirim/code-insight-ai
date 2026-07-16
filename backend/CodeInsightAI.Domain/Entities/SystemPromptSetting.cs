namespace CodeInsightAI.Domain.Entities;

// Single-row settings table: holds the AI's base system prompt when the user has customized it
// from the built-in default via the app's settings screen, instead of it being hardcoded and only
// changeable by editing source code and redeploying.
public class SystemPromptSetting
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
