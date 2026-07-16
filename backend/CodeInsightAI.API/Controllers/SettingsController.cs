using CodeInsightAI.Application.AI;
using CodeInsightAI.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CodeInsightAI.API.Controllers;

// Lets the AI's base system prompt be edited from within the app instead of only by changing
// source code and redeploying. The prompt still always gets the JSON response schema enforced
// separately by each AI provider, so editing this text can't break the structured report shape.
[ApiController]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
    private readonly ISystemPromptRepository _systemPromptRepository;

    public SettingsController(ISystemPromptRepository systemPromptRepository)
    {
        _systemPromptRepository = systemPromptRepository;
    }

    [HttpGet("prompt")]
    public async Task<IActionResult> GetPrompt()
    {
        try
        {
            var customPrompt = await _systemPromptRepository.GetCustomPromptAsync();
            return Ok(new
            {
                prompt = customPrompt ?? DefaultSystemPrompt.Text,
                isDefault = customPrompt == null,
                defaultPrompt = DefaultSystemPrompt.Text
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Prompt alınırken sunucu hatası oluştu.", error = ex.Message });
        }
    }

    [HttpPut("prompt")]
    public async Task<IActionResult> SavePrompt([FromBody] SavePromptRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest(new { message = "Prompt boş olamaz." });
        }

        try
        {
            await _systemPromptRepository.SaveCustomPromptAsync(request.Prompt);
            return Ok(new { saved = true });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Prompt kaydedilirken sunucu hatası oluştu.", error = ex.Message });
        }
    }

    [HttpPost("prompt/reset")]
    public async Task<IActionResult> ResetPrompt()
    {
        try
        {
            await _systemPromptRepository.ResetToDefaultAsync();
            return Ok(new { prompt = DefaultSystemPrompt.Text, isDefault = true });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Prompt sıfırlanırken sunucu hatası oluştu.", error = ex.Message });
        }
    }
}

public class SavePromptRequest
{
    public string Prompt { get; set; } = string.Empty;
}
