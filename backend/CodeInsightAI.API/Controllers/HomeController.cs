using Microsoft.AspNetCore.Mvc;

namespace CodeInsightAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HomeController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            Project = "CodeInsight AI",
            Version = "1.0.0",
            Status = "Running"
        });
    }
}