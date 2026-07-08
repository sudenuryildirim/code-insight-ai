namespace CodeInsightAI.Application.DTOs;

public class ReviewRequestDto
{
    public string Code { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    // Not set by the user: the AI detects the programming language itself from
    // the source code (and the file extension, if available) during analysis.
}
