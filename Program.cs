using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using UglyToad.PdfPig;
using resume_analyzer.Models;
using resume_analyzer.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddHttpClient<OpenAiClient>();
builder.Services.AddRazorPages();
builder.Services.AddAntiforgery();

// Add rate limiting service
builder.Services.AddSingleton<RateLimitingService>();
// Add analysis cache service
builder.Services.AddSingleton<AnalysisCacheService>();
// Add usage tracking service
builder.Services.AddSingleton<UsageTrackingService>();
// Add budget monitoring service
builder.Services.AddSingleton<BudgetMonitoringService>();
// Add feedback service
builder.Services.AddSingleton<FeedbackService>();
// Add privacy-safe user interaction logging service
builder.Services.AddSingleton<InteractionLogService>();

var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

// Load usage data on startup
var usageService = app.Services.GetRequiredService<UsageTrackingService>();
usageService.LoadUsageFromFile();

// Load feedback data on startup
var feedbackService = app.Services.GetRequiredService<FeedbackService>();
feedbackService.LoadFeedbackFromFile();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAntiforgery();

// Add rate limiting middleware
app.UseMiddleware<RateLimitingMiddleware>();

app.MapPost("/api/interaction-log", (InteractionLogRequest request, HttpContext context, InteractionLogService interactionLog) =>
{
    if (string.IsNullOrWhiteSpace(request.EventName) || request.EventName.Length > 80)
    {
        return Results.BadRequest();
    }

    var details = new Dictionary<string, object?>();
    foreach (var item in request.Details ?? new Dictionary<string, JsonElement>())
    {
        if (item.Key.Length > 80)
        {
            continue;
        }

        details[item.Key] = item.Value.ValueKind switch
        {
            JsonValueKind.String => item.Value.GetString() is { Length: <= 120 } value ? value : "[redacted-long-string]",
            JsonValueKind.Number when item.Value.TryGetInt64(out var number) => number,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => item.Value.ToString().Length <= 120 ? item.Value.ToString() : "[redacted-long-value]"
        };
    }

    interactionLog.Log(request.EventName, context, details, request.SessionId);
    return Results.NoContent();
});

// Map Razor Pages
app.MapRazorPages();

// Keep the API endpoint for backwards compatibility
app.MapPost("/api/analyze-resume", async (HttpRequest httpRequest, OpenAiClient openAiClient) =>
{
    var openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    if (string.IsNullOrWhiteSpace(openAiApiKey))
    {
        app.Logger.LogError("OPENAI_API_KEY is not set.");
        return Results.Problem("OPENAI_API_KEY is not set on the host.", statusCode: 500);
    }

    string? targetRole = null;
    string? resumeText = null;

    if (httpRequest.HasFormContentType)
    {
        var form = await httpRequest.ReadFormAsync();
        targetRole = form["targetRole"].ToString();
        resumeText = form["resumeText"].ToString();

        var resumeFile = form.Files.GetFile("resumeFile");
        if (resumeFile is not null && resumeFile.Length > 0)
        {
            resumeText = await ExtractTextFromFileAsync(resumeFile);
        }
    }
    else
    {
        var request = await JsonSerializer.DeserializeAsync<ResumeAnalysisRequest>(httpRequest.Body);
        targetRole = request?.TargetRole;
        resumeText = request?.ResumeText;
    }

    if (string.IsNullOrWhiteSpace(targetRole) || string.IsNullOrWhiteSpace(resumeText))
    {
        return Results.BadRequest(new { error = "A resume file or resumeText plus targetRole is required." });
    }

    try
    {
        var analysisRequest = new ResumeAnalysisRequest
        {
            ResumeText = resumeText,
            TargetRole = targetRole
        };

        var analysis = await openAiClient.AnalyzeResumeAsync(analysisRequest, openAiApiKey);
        return Results.Ok(analysis);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Resume analysis failed.");
        return Results.Problem("Unable to analyze the resume at this time.");
    }
});

app.Run();

static async Task<string> ExtractTextFromFileAsync(IFormFile file)
{
    if (file.Length == 0)
    {
        return string.Empty;
    }

    var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();

    await using var stream = file.OpenReadStream();

    if (ext == ".pdf")
    {
        using var document = PdfDocument.Open(stream);
        var builder = new StringBuilder();
        foreach (var page in document.GetPages())
        {
            builder.AppendLine(page.Text);
        }
        return builder.ToString().Trim();
    }

    using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
    return await reader.ReadToEndAsync();
}
