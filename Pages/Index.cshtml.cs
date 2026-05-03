using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using resume_analyzer.Models;
using resume_analyzer.Services;
using System.Text;
using UglyToad.PdfPig;

namespace resume_analyzer.Pages;

public class IndexModel : PageModel
{
    private readonly OpenAiClient _openAiClient;
    private readonly BudgetMonitoringService _budgetService;
    private readonly InteractionLogService _interactionLog;
    private readonly ILogger<IndexModel> _logger;

    // Guardrail constants
    private const int MaxTextLength = 10000; // 10,000 characters
    private const long MaxFileSize = 5 * 1024 * 1024; // 5MB
    private const int MaxTokensPerRequest = 2000; // Token limit
    private const int TokensPerCharacter = 4; // Rough estimate: 1 token ≈ 4 chars

    public string? ErrorMessage { get; set; }
    public int EstimatedTokens { get; set; }
    public bool IsOverLimit { get; set; }

    public IndexModel(OpenAiClient openAiClient, BudgetMonitoringService budgetService, InteractionLogService interactionLog, ILogger<IndexModel> logger)
    {
        _openAiClient = openAiClient;
        _budgetService = budgetService;
        _interactionLog = interactionLog;
        _logger = logger;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            var targetRole = Request.Form["targetRole"].ToString();
            var resumeText = Request.Form["resumeText"].ToString();
            var resumeFile = Request.Form.Files.GetFile("resumeFile");
            var sessionId = Request.Form["interactionSessionId"].ToString();
            var submittedWithFile = resumeFile is not null && resumeFile.Length > 0;

            LogSubmitEvent("server_submit_received", sessionId, targetRole, resumeText, resumeFile);

            if (string.IsNullOrWhiteSpace(targetRole))
            {
                LogValidationEvent("server_validation_failed", sessionId, "missing_target_role", targetRole, resumeText, resumeFile);
                ErrorMessage = "Please enter a target role.";
                return Page();
            }

            // Extract text from file if uploaded
            if (resumeFile is not null && resumeFile.Length > 0)
            {
                // Validate file size
                if (resumeFile.Length > MaxFileSize)
                {
                    LogValidationEvent("server_validation_failed", sessionId, "file_too_large", targetRole, resumeText, resumeFile);
                    ErrorMessage = $"File too large. Maximum size is 5MB. Your file is {(resumeFile.Length / 1024 / 1024):F1}MB.";
                    return Page();
                }

                resumeText = await ExtractTextFromFileAsync(resumeFile);
                LogSubmitEvent("server_file_text_extracted", sessionId, targetRole, resumeText, resumeFile);
            }

            if (string.IsNullOrWhiteSpace(resumeText))
            {
                LogValidationEvent("server_validation_failed", sessionId, "missing_resume", targetRole, resumeText, resumeFile);
                ErrorMessage = "Please upload a resume file or paste resume text.";
                return Page();
            }

            // Validate text length
            if (resumeText.Length > MaxTextLength)
            {
                LogValidationEvent("server_validation_failed", sessionId, "resume_text_too_long", targetRole, resumeText, resumeFile);
                ErrorMessage = $"Resume text too long. Maximum {MaxTextLength} characters. Your text has {resumeText.Length} characters.";
                return Page();
            }

            // Estimate tokens
            EstimatedTokens = EstimateTokens(resumeText + targetRole);
            IsOverLimit = EstimatedTokens > MaxTokensPerRequest;

            if (IsOverLimit)
            {
                LogValidationEvent("server_validation_failed", sessionId, "token_limit_exceeded", targetRole, resumeText, resumeFile);
                ErrorMessage = $"Estimated token usage ({EstimatedTokens}) exceeds limit ({MaxTokensPerRequest}). Please shorten your resume or target role.";
                return Page();
            }

            var openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (string.IsNullOrWhiteSpace(openAiApiKey))
            {
                _logger.LogError("OPENAI_API_KEY is not set.");
                LogValidationEvent("server_analysis_blocked", sessionId, "missing_api_key", targetRole, resumeText, resumeFile);
                ErrorMessage = "API configuration error. Please try again later.";
                return Page();
            }

            // Check budget before proceeding
            var budgetStatus = _budgetService.CheckBudget();
            if (budgetStatus.IsOverLimit)
            {
                LogValidationEvent("server_analysis_blocked", sessionId, "budget_exceeded", targetRole, resumeText, resumeFile);
                ErrorMessage = $"Monthly budget exceeded (${budgetStatus.CurrentSpend:F2} of ${budgetStatus.BudgetLimit:F2}). Please try again next month.";
                return Page();
            }

            var useSimplified = budgetStatus.PercentageUsed >= 90; // If over 90%, use simplified
            _interactionLog.Log("server_analysis_started", HttpContext, BuildInteractionDetails(targetRole, resumeText, resumeFile, useSimplified), sessionId);

            var analysisRequest = new ResumeAnalysisRequest
            {
                ResumeText = resumeText,
                TargetRole = targetRole
            };

            var analysis = await _openAiClient.AnalyzeResumeAsync(analysisRequest, openAiApiKey, useSimplified);
            _interactionLog.Log("server_analysis_completed", HttpContext, BuildInteractionDetails(targetRole, resumeText, resumeFile, useSimplified), sessionId);

            // Store result in TempData for results page
            TempData["AnalysisResult"] = System.Text.Json.JsonSerializer.Serialize(analysis);
            TempData["TargetRole"] = targetRole;
            TempData["SubmittedWithFile"] = submittedWithFile ? "true" : "false";
            TempData["InteractionSessionId"] = sessionId;

            return RedirectToPage("Results");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Resume processing issue.");
            _interactionLog.Log("server_processing_failed", HttpContext, new Dictionary<string, object?> { ["errorType"] = ex.GetType().Name }, Request.Form["interactionSessionId"].ToString());
            ErrorMessage = ex.Message;
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Resume analysis failed.");
            _interactionLog.Log("server_analysis_failed", HttpContext, new Dictionary<string, object?> { ["errorType"] = ex.GetType().Name }, Request.Form["interactionSessionId"].ToString());
            ErrorMessage = "Failed to analyze resume. Please try again.";
            return Page();
        }
    }

    private void LogSubmitEvent(string eventName, string sessionId, string targetRole, string resumeText, IFormFile? resumeFile)
    {
        _interactionLog.Log(eventName, HttpContext, BuildInteractionDetails(targetRole, resumeText, resumeFile), sessionId);
    }

    private void LogValidationEvent(string eventName, string sessionId, string reason, string targetRole, string resumeText, IFormFile? resumeFile)
    {
        var details = BuildInteractionDetails(targetRole, resumeText, resumeFile);
        details["reason"] = reason;
        details["estimatedTokens"] = EstimatedTokens;
        _interactionLog.Log(eventName, HttpContext, details, sessionId);
    }

    private static Dictionary<string, object?> BuildInteractionDetails(string targetRole, string resumeText, IFormFile? resumeFile, bool? useSimplifiedAnalysis = null)
    {
        var details = new Dictionary<string, object?>
        {
            ["targetRoleLength"] = targetRole.Trim().Length,
            ["resumeTextLength"] = resumeText.Trim().Length,
            ["hasFile"] = resumeFile is not null && resumeFile.Length > 0,
            ["hasPastedText"] = !string.IsNullOrWhiteSpace(resumeText),
            ["fileSizeBytes"] = resumeFile?.Length ?? 0,
            ["fileExtension"] = string.IsNullOrWhiteSpace(resumeFile?.FileName) ? string.Empty : Path.GetExtension(resumeFile.FileName).ToLowerInvariant()
        };

        if (useSimplifiedAnalysis.HasValue)
        {
            details["useSimplifiedAnalysis"] = useSimplifiedAnalysis.Value;
        }

        return details;
    }

    private static int EstimateTokens(string text)
    {
        return (int)Math.Ceiling(text.Length / (double)TokensPerCharacter);
    }

    private static async Task<string> ExtractTextFromFileAsync(IFormFile file)
    {
        if (file.Length == 0)
        {
            return string.Empty;
        }

        var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();

        try
        {
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
        catch (Exception ex)
        {
            throw new InvalidOperationException("Couldn’t process file. Try a text-based PDF or paste resume text.", ex);
        }
    }
}
