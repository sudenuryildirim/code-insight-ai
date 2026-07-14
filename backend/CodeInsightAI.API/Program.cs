using System.Text.Json.Serialization;
using CodeInsightAI.API.Hubs;
using CodeInsightAI.Application.DependencyInjection;
using CodeInsightAI.Infrastructure.DependencyInjection;
using CodeInsightAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// QuestPDF Community license (free for individuals / small businesses under its revenue threshold).
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Serialize enums (IssueSeverity, IssueCategory) as strings so the Angular
        // frontend can round-trip a report (analyze -> display -> report/pdf) without
        // needing to know the underlying numeric values.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

// Register Clean Architecture layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Configure CORS. Falls back to the Angular dev server if Cors:AllowedOrigins isn't set, so local
// development keeps working unconfigured; production sets it to the deployed frontend's real origin(s).
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:4200" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // required for the SignalR hub connection (PullRequestHub)
    });
});

var app = builder.Build();

// Apply pending EF Core migrations on startup so the SQLite file/schema always matches the code
// without requiring a manual `dotnet ef database update` step.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowFrontend");

app.MapControllers();
app.MapHub<PullRequestHub>("/hubs/pull-requests");

app.Run();
