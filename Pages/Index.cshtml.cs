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
    private readonly ILogger<IndexModel> _logger;

    public string? ErrorMessage { get; set; }

    public IndexModel(OpenAiClient openAiClient, ILogger<IndexModel> logger)
    {
        _openAiClient = openAiClient;
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
                resumeText = await ExtractTextFromFileAsync(resumeFile);
            }

            if (string.IsNullOrWhiteSpace(resumeText))
            {
                ErrorMessage = "Please upload a resume file or paste resume text.";
                return Page();
            }

            var openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (string.IsNullOrWhiteSpace(openAiApiKey))
            {
                _logger.LogError("OPENAI_API_KEY is not set.");
                ErrorMessage = "API configuration error. Please try again later.";
                return Page();
            }

            var analysisRequest = new ResumeAnalysisRequest
            {
                ResumeText = resumeText,
                TargetRole = targetRole
            };

            var analysis = await _openAiClient.AnalyzeResumeAsync(analysisRequest, openAiApiKey);

            // Store result in TempData for results page
            TempData["AnalysisResult"] = System.Text.Json.JsonSerializer.Serialize(analysis);
            TempData["TargetRole"] = targetRole;

            return RedirectToPage("Results");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Resume analysis failed.");
            ErrorMessage = "Failed to analyze resume. Please try again.";
            return Page();
        }
    }

    private static async Task<string> ExtractTextFromFileAsync(IFormFile file)
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
}
