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
    private readonly ILogger<IndexModel> _logger;

    // Guardrail constants
    private const int MaxTextLength = 10000; // 10,000 characters
    private const long MaxFileSize = 5 * 1024 * 1024; // 5MB
    private const int MaxTokensPerRequest = 2000; // Token limit
    private const int TokensPerCharacter = 4; // Rough estimate: 1 token ≈ 4 chars

    public string? ErrorMessage { get; set; }
    public int EstimatedTokens { get; set; }
    public bool IsOverLimit { get; set; }

    public IndexModel(OpenAiClient openAiClient, BudgetMonitoringService budgetService, ILogger<IndexModel> logger)
    {
        _openAiClient = openAiClient;
        _budgetService = budgetService;
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

            if (string.IsNullOrWhiteSpace(targetRole))
            {
                ErrorMessage = "Please enter a target role.";
                return Page();
            }

            // Extract text from file if uploaded
            if (resumeFile is not null && resumeFile.Length > 0)
            {
                // Validate file size
                if (resumeFile.Length > MaxFileSize)
                {
                    ErrorMessage = $"File too large. Maximum size is 5MB. Your file is {(resumeFile.Length / 1024 / 1024):F1}MB.";
                    return Page();
                }

                resumeText = await ExtractTextFromFileAsync(resumeFile);
            }

            if (string.IsNullOrWhiteSpace(resumeText))
            {
                ErrorMessage = "Please upload a resume file or paste resume text.";
                return Page();
            }

            // Validate text length
            if (resumeText.Length > MaxTextLength)
            {
                ErrorMessage = $"Resume text too long. Maximum {MaxTextLength} characters. Your text has {resumeText.Length} characters.";
                return Page();
            }

            // Estimate tokens
            EstimatedTokens = EstimateTokens(resumeText + targetRole);
            IsOverLimit = EstimatedTokens > MaxTokensPerRequest;

            if (IsOverLimit)
            {
                ErrorMessage = $"Estimated token usage ({EstimatedTokens}) exceeds limit ({MaxTokensPerRequest}). Please shorten your resume or target role.";
                return Page();
            }

            var openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (string.IsNullOrWhiteSpace(openAiApiKey))
            {
                _logger.LogError("OPENAI_API_KEY is not set.");
                ErrorMessage = "API configuration error. Please try again later.";
                return Page();
            }

            // Check budget before proceeding
            var budgetStatus = _budgetService.CheckBudget();
            if (budgetStatus.IsOverLimit)
            {
                ErrorMessage = $"Monthly budget exceeded (${budgetStatus.CurrentSpend:F2} of ${budgetStatus.BudgetLimit:F2}). Please try again next month.";
                return Page();
            }

            var useSimplified = budgetStatus.PercentageUsed >= 90; // If over 90%, use simplified

            var analysisRequest = new ResumeAnalysisRequest
            {
                ResumeText = resumeText,
                TargetRole = targetRole
            };

            var analysis = await _openAiClient.AnalyzeResumeAsync(analysisRequest, openAiApiKey, useSimplified);

            // Store result in TempData for results page
            TempData["AnalysisResult"] = System.Text.Json.JsonSerializer.Serialize(analysis);
            TempData["TargetRole"] = targetRole;

            return RedirectToPage("Results");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Resume processing issue.");
            ErrorMessage = ex.Message;
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Resume analysis failed.");
            ErrorMessage = "Failed to analyze resume. Please try again.";
            return Page();
        }
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
