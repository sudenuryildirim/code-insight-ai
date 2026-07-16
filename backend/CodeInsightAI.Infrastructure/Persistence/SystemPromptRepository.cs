using CodeInsightAI.Application.Interfaces;
using CodeInsightAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeInsightAI.Infrastructure.Persistence;

public class SystemPromptRepository : ISystemPromptRepository
{
    // Always the same single row - there's exactly one active system prompt for the whole app.
    private const int SettingId = 1;

    private readonly AppDbContext _dbContext;

    public SystemPromptRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<string?> GetCustomPromptAsync()
    {
        var setting = await _dbContext.SystemPromptSettings.FirstOrDefaultAsync(s => s.Id == SettingId);
        return string.IsNullOrWhiteSpace(setting?.Content) ? null : setting.Content;
    }

    public async Task SaveCustomPromptAsync(string content)
    {
        var setting = await _dbContext.SystemPromptSettings.FirstOrDefaultAsync(s => s.Id == SettingId);
        if (setting == null)
        {
            _dbContext.SystemPromptSettings.Add(new SystemPromptSetting { Id = SettingId, Content = content, UpdatedAt = DateTime.UtcNow });
        }
        else
        {
            setting.Content = content;
            setting.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync();
    }

    public async Task ResetToDefaultAsync()
    {
        var setting = await _dbContext.SystemPromptSettings.FirstOrDefaultAsync(s => s.Id == SettingId);
        if (setting != null)
        {
            _dbContext.SystemPromptSettings.Remove(setting);
            await _dbContext.SaveChangesAsync();
        }
    }
}
