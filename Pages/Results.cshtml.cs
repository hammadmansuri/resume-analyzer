using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using resume_analyzer.Models;
using resume_analyzer.Services;
using System.Text;
using System.Text.Json;

namespace resume_analyzer.Pages;

public class ResultsModel : PageModel
{
    private readonly FeedbackService _feedbackService;

    public ResumeAnalysisResponse? Analysis { get; set; }
    public string? TargetRole { get; set; }

    public ResultsModel(FeedbackService feedbackService)
    {
        _feedbackService = feedbackService;
    }

    public IActionResult OnGet(bool? download)
    {
        if (TempData.Peek("AnalysisResult") is string analysisJson)
        {
            try
            {
                var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    PropertyNameCaseInsensitive = true
                };
                Analysis = JsonSerializer.Deserialize<ResumeAnalysisResponse>(analysisJson, options);
                TargetRole = TempData.Peek("TargetRole")?.ToString();
                TempData.Keep("AnalysisResult");
                TempData.Keep("TargetRole");
            }
            catch
            {
                // If deserialization fails, analysis will be null
            }
        }

        if (download == true && Analysis != null)
        {
            var report = GenerateReport();
            var bytes = Encoding.UTF8.GetBytes(report);
            return File(bytes, "text/html", "resume-analysis-report.html");
        }

        return Page();
    }

    public IActionResult OnPost(int rating, string comments, bool helpful)
    {
        if (Analysis != null)
        {
            var feedback = new FeedbackService.FeedbackRecord
            {
                TargetRole = TargetRole,
                Score = Analysis.Score,
                Rating = rating,
                Comments = comments,
                Helpful = helpful
            };
            _feedbackService.AddFeedback(feedback);
        }

        // Redirect back to results with success message
        TempData["FeedbackSubmitted"] = "Thank you for your feedback!";
        return RedirectToPage();
    }

    private string GenerateReport()
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine("<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\">\n<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n<title>Resume Analysis Report</title>\n<style>");
        builder.AppendLine("body { font-family: Inter, system-ui, sans-serif; margin: 0; padding: 32px; background: #f3f4f6; color: #111827; }");
        builder.AppendLine(".report-card { background: white; border-radius: 18px; padding: 32px; max-width: 960px; margin: auto; box-shadow: 0 24px 80px rgba(15, 23, 42, 0.08); }");
        builder.AppendLine("h1, h2, h3 { margin-top: 0; color: #111827; }");
        builder.AppendLine(".subtitle { color: #6b7280; margin-bottom: 24px; }");
        builder.AppendLine("p, li { line-height: 1.75; }");
        builder.AppendLine("ul { padding-left: 20px; margin: 12px 0; }");
        builder.AppendLine(".section { margin-bottom: 28px; }");
        builder.AppendLine(".meta { display: grid; gap: 14px; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); margin-top: 16px; }");
        builder.AppendLine(".meta div { background: #f9fafb; border-radius: 16px; padding: 18px 20px; }");
        builder.AppendLine(".meta strong { display: block; margin-bottom: 8px; color: #374151; font-size: 14px; }");
        builder.AppendLine(".meta span { display: block; font-size: 20px; font-weight: 700; color: #111827; }");
        builder.AppendLine(".badge { display: inline-block; margin: 0 6px 6px 0; padding: 8px 12px; border-radius: 999px; background: #e5e7eb; color: #374151; font-size: 13px; }");
        builder.AppendLine(".list-card { background: #f9fafb; border-radius: 16px; padding: 20px; }");
        builder.AppendLine(".list-card li { margin-bottom: 12px; }");
        builder.AppendLine(".section-note { color: #6b7280; margin-top: 8px; }");
        builder.AppendLine("</style>\n</head>\n<body>\n<div class=\"report-card\">\n");
        builder.AppendLine($"<h1>Resume Analysis Report</h1>\n<p class=\"subtitle\">Target role: <strong>{TargetRole}</strong></p>\n");
        builder.AppendLine("<section class=\"section\"><h2>Overview</h2>\n<div class=\"meta\">\n");
        builder.AppendLine($"<div><strong>Score</strong><span>{Analysis?.Score}</span></div>\n");
        builder.AppendLine($"<div><strong>Fit</strong><span>{Analysis?.RoleAssessment.Fit}</span></div>\n");
        builder.AppendLine($"<div><strong>Suggested role</strong><span>{Analysis?.RoleAssessment.SuggestedRole}</span></div>\n");
        builder.AppendLine($"<div><strong>Confidence</strong><span>{Analysis?.RoleAssessment.Confidence}</span></div>\n");
        builder.AppendLine("</div>\n");
        if (!string.IsNullOrEmpty(Analysis?.RoleAssessment.Reason))
        {
            builder.AppendLine($"<p>{Analysis.RoleAssessment.Reason}</p>\n");
        }
        builder.AppendLine("</section>\n");

        if (Analysis?.Strengths?.Count > 0)
        {
            builder.AppendLine("<section class=\"section\"><h2>Strengths</h2>\n<ul class=\"list-card\">\n");
            foreach (var strength in Analysis.Strengths)
            {
                builder.AppendLine($"<li>{System.Net.WebUtility.HtmlEncode(strength)}</li>\n");
            }
            builder.AppendLine("</ul>\n</section>\n");
        }

        if (Analysis?.MissingSkills != null)
        {
            builder.AppendLine("<section class=\"section\"><h2>Missing Skills</h2>\n");
            if (Analysis.MissingSkills.MustHave?.Count > 0)
            {
                builder.AppendLine("<h3>Must-Have</h3>\n<ul class=\"list-card\">\n");
                foreach (var item in Analysis.MissingSkills.MustHave)
                {
                    builder.AppendLine($"<li><strong>{System.Net.WebUtility.HtmlEncode(item.Skill)}</strong>: {System.Net.WebUtility.HtmlEncode(item.Reason)}</li>\n");
                }
                builder.AppendLine("</ul>\n");
            }
            if (Analysis.MissingSkills.GoodToHave?.Count > 0)
            {
                builder.AppendLine("<h3>Good-to-Have</h3>\n<ul class=\"list-card\">\n");
                foreach (var item in Analysis.MissingSkills.GoodToHave)
                {
                    builder.AppendLine($"<li><strong>{System.Net.WebUtility.HtmlEncode(item.Skill)}</strong>: {System.Net.WebUtility.HtmlEncode(item.Reason)}</li>\n");
                }
                builder.AppendLine("</ul>\n");
            }
            builder.AppendLine("</section>\n");
        }

        if (Analysis?.ActionPlan?.Count > 0)
        {
            builder.AppendLine("<section class=\"section\"><h2>Action Plan</h2>\n<p class=\"section-note\">Follow this sequence to close key gaps.</p>\n<ul class=\"list-card\">\n");
            foreach (var step in Analysis.ActionPlan)
            {
                builder.AppendLine($"<li><strong>Step {step.Step}:</strong> {System.Net.WebUtility.HtmlEncode(step.Task)}<br><span class=\"badge\">Goal: {System.Net.WebUtility.HtmlEncode(step.Goal)}</span><span class=\"badge\">Difficulty: {System.Net.WebUtility.HtmlEncode(step.Difficulty)}</span><span class=\"badge\">Time: {System.Net.WebUtility.HtmlEncode(step.Time)}</span></li>\n");
            }
            builder.AppendLine("</ul>\n</section>\n");
        }
        else if (Analysis?.Actions?.Count > 0)
        {
            builder.AppendLine("<section class=\"section\"><h2>Action Plan</h2>\n<p class=\"section-note\">Follow this sequence to close key gaps.</p>\n<ul class=\"list-card\">\n");
            foreach (var (action, index) in Analysis.Actions.Select((a, i) => (a, i + 1)))
            {
                builder.AppendLine($"<li><strong>Step {index}:</strong> {System.Net.WebUtility.HtmlEncode(action.Task)}<br><span class=\"badge\">Difficulty: {System.Net.WebUtility.HtmlEncode(action.Difficulty)}</span><span class=\"badge\">Time: {System.Net.WebUtility.HtmlEncode(action.Time)}</span></li>\n");
            }
            builder.AppendLine("</ul>\n</section>\n");
        }

        if (!string.IsNullOrEmpty(Analysis?.FirstStep))
        {
            builder.AppendLine("<section class=\"section\"><h2>First Step</h2>\n<div class=\"list-card\">\n");
            builder.AppendLine($"<p>{System.Net.WebUtility.HtmlEncode(Analysis.FirstStep)}</p>\n");
            builder.AppendLine("</div>\n</section>\n");
        }

        builder.AppendLine("</div>\n</body>\n</html>");
        return builder.ToString();
    }

    public string GetScoreClass()
    {
        return Analysis?.Score switch
        {
            >= 80 => "score-excellent",
            >= 65 => "score-good",
            >= 50 => "score-fair",
            _ => "score-low"
        };
    }

    public string GetScoreInterpretation()
    {
        return Analysis?.Score switch
        {
            >= 85 => "Exceptional match for this role. You're well-prepared.",
            >= 75 => "Strong candidate. Minor skill gaps to address.",
            >= 65 => "Good foundation. Several skills to develop.",
            >= 50 => "Moderate match. Significant learning needed.",
            _ => "Early stage for this role. Focus on fundamentals."
        };
    }

    public string GetFitClass()
    {
        return Analysis?.RoleAssessment?.Fit?.ToLower() switch
        {
            "well-matched" or "good-fit" => "fit-good",
            "over-qualified" => "fit-overqualified",
            "under-qualified" => "fit-underqualified",
            _ => "fit-unknown"
        };
    }

    public string GetFitLabel()
    {
        return Analysis?.RoleAssessment?.Fit?.ToLower() switch
        {
            "well-matched" or "good-fit" => "✓ Well Matched",
            "over-qualified" => "⬆️ Over-Qualified",
            "under-qualified" => "⬇️ Under-Qualified",
            _ => "Assessment"
        };
    }
}
