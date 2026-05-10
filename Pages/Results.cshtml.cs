using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using resume_analyzer.Models;
using resume_analyzer.Services;
using System.Net;
using System.Text;
using System.Text.Json;

namespace resume_analyzer.Pages;

public class ResultsModel : PageModel
{
    private readonly FeedbackService _feedbackService;
    private readonly InteractionLogService _interactionLog;

    public ResumeAnalysisResponse? Analysis { get; set; }
    public string? TargetRole { get; set; }
    public string InteractionSessionId { get; set; } = string.Empty;
    public string SubmittedWithFile { get; set; } = string.Empty;

    public ResultsModel(FeedbackService feedbackService, InteractionLogService interactionLog)
    {
        _feedbackService = feedbackService;
        _interactionLog = interactionLog;
    }

    public IActionResult OnGet(bool? download)
    {
        LoadAnalysisFromTempData();

        if (download == true && Analysis != null)
        {
            LogResultsEvent("server_results_downloaded");
            var report = GenerateReport();
            var bytes = Encoding.UTF8.GetBytes(report);
            return File(bytes, "text/html", "resume-analysis-report.html");
        }

        LogResultsEvent(Analysis != null ? "server_results_page_loaded" : "server_results_page_missing_analysis");
        return Page();
    }

    public IActionResult OnPost(int rating, string comments, bool helpful, string? targetRole, int? score)
    {
        LoadAnalysisFromTempData();

        var feedbackTargetRole = TargetRole ?? targetRole;
        var feedbackScore = Analysis?.Score ?? score;

        if (feedbackScore.HasValue)
        {
            var feedback = new FeedbackService.FeedbackRecord
            {
                TargetRole = feedbackTargetRole,
                Score = feedbackScore.Value,
                Rating = rating,
                Comments = comments,
                Helpful = helpful
            };
            _feedbackService.AddFeedback(feedback);
            _interactionLog.Log("server_feedback_submitted", HttpContext, new Dictionary<string, object?>
            {
                ["targetRoleLength"] = (feedbackTargetRole ?? string.Empty).Length,
                ["score"] = feedbackScore.Value,
                ["rating"] = rating,
                ["helpful"] = helpful,
                ["commentLength"] = comments?.Length ?? 0
            }, InteractionSessionId);

            TempData["FeedbackSubmitted"] = "Thank you for your feedback!";
        }
        else
        {
            _interactionLog.Log("server_feedback_failed", HttpContext, new Dictionary<string, object?>
            {
                ["reason"] = "missing_analysis_details",
                ["rating"] = rating,
                ["helpful"] = helpful,
                ["commentLength"] = comments?.Length ?? 0
            }, InteractionSessionId);
            TempData["FeedbackSubmitted"] = "Unable to save feedback because the analysis details expired. Please run a new analysis and try again.";
        }

        return RedirectToPage();
    }

    private void LoadAnalysisFromTempData()
    {
        if (TempData.Peek("AnalysisResult") is not string analysisJson)
        {
            return;
        }

        try
        {
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                PropertyNameCaseInsensitive = true
            };
            Analysis = JsonSerializer.Deserialize<ResumeAnalysisResponse>(analysisJson, options);
            if (Analysis != null)
            {
                ResumeAnalyzerService.EnsureFirstStep(Analysis);
            }

            TargetRole = TempData.Peek("TargetRole")?.ToString();
            InteractionSessionId = TempData.Peek("InteractionSessionId")?.ToString() ?? string.Empty;
            SubmittedWithFile = TempData.Peek("SubmittedWithFile")?.ToString() ?? string.Empty;
            TempData.Keep("AnalysisResult");
            TempData.Keep("TargetRole");
            TempData.Keep("InteractionSessionId");
            TempData.Keep("SubmittedWithFile");
        }
        catch
        {
            // If deserialization fails, analysis will be null
        }
    }

    private void LogResultsEvent(string eventName)
    {
        _interactionLog.Log(eventName, HttpContext, new Dictionary<string, object?>
        {
            ["hasAnalysis"] = Analysis != null,
            ["targetRoleLength"] = TargetRole?.Length ?? 0,
            ["score"] = Analysis?.Score,
            ["fit"] = Analysis?.RoleAssessment?.Fit ?? string.Empty,
            ["hasFirstStep"] = !string.IsNullOrWhiteSpace(Analysis?.FirstStep?.Task),
            ["actionPlanCount"] = Analysis?.ActionPlan?.Count ?? 0,
            ["mustHaveCount"] = Analysis?.MissingSkills?.MustHave?.Count ?? 0,
            ["goodToHaveCount"] = Analysis?.MissingSkills?.GoodToHave?.Count ?? 0,
            ["submittedWithFile"] = SubmittedWithFile
        }, InteractionSessionId);
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
        builder.AppendLine($"<h1>Resume Analysis Report</h1>\n<p class=\"subtitle\">Target role: <strong>{Encode(TargetRole)}</strong></p>\n");

        builder.AppendLine("<section class=\"section\"><h2>Overview</h2>\n<div class=\"meta\">\n");
        builder.AppendLine($"<div><strong>Score</strong><span>{Analysis?.Score}</span></div>\n");
        builder.AppendLine($"<div><strong>Fit</strong><span>{Encode(Analysis?.RoleAssessment.Fit)}</span></div>\n");
        builder.AppendLine($"<div><strong>Suggested role</strong><span>{Encode(Analysis?.RoleAssessment.SuggestedRole)}</span></div>\n");
        builder.AppendLine($"<div><strong>Confidence</strong><span>{Encode(Analysis?.RoleAssessment.Confidence)}</span></div>\n");
        builder.AppendLine("</div>\n");
        builder.AppendLine($"<p class=\"section-note\"><strong>Readiness:</strong> {Encode(GetScoreInterpretation())}</p>\n");
        if (!string.IsNullOrEmpty(Analysis?.RoleAssessment.Reason))
        {
            builder.AppendLine($"<p>{Encode(Analysis.RoleAssessment.Reason)}</p>\n");
        }
        builder.AppendLine("</section>\n");

        if (!string.IsNullOrWhiteSpace(Analysis?.QuickSummary))
        {
            builder.AppendLine("<section class=\"section\"><h2>⚡ Quick Summary</h2>\n<ul class=\"list-card\">\n");
            foreach (var line in Analysis.QuickSummary.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                builder.AppendLine($"<li>{Encode(line.Trim())}</li>\n");
            }
            builder.AppendLine("</ul>\n</section>\n");
        }

        if (Analysis?.QuickWins?.Count > 0)
        {
            builder.AppendLine("<section class=\"section\"><h2>🚀 Quick Wins</h2>\n<ul class=\"list-card\">\n");
            foreach (var win in Analysis.QuickWins)
            {
                builder.AppendLine($"<li>{Encode(win)}</li>\n");
            }
            builder.AppendLine("</ul>\n</section>\n");
        }

        if (Analysis?.ResumeTips?.Count > 0)
        {
            builder.AppendLine("<section class=\"section\"><h2>📝 Resume Tips</h2>\n<ul class=\"list-card\">\n");
            foreach (var tip in Analysis.ResumeTips)
            {
                builder.AppendLine($"<li>{Encode(tip)}</li>\n");
            }
            builder.AppendLine("</ul>\n</section>\n");
        }

        if (Analysis?.FirstStep != null && !string.IsNullOrWhiteSpace(Analysis.FirstStep.Task))
        {
            builder.AppendLine("<section class=\"section\"><h2>🚀 Start Here (Do this first)</h2>\n<div class=\"list-card\">\n");
            builder.AppendLine($"<p><strong>Task:</strong> {Encode(Analysis.FirstStep.Task)}</p>\n");
            builder.AppendLine($"<p><strong>Time:</strong> {Encode(Analysis.FirstStep.Time)}</p>\n");
            builder.AppendLine($"<p><strong>Outcome:</strong> {Encode(Analysis.FirstStep.Outcome)}</p>\n");
            if (!string.IsNullOrWhiteSpace(Analysis.FirstStep.Resource))
            {
                builder.AppendLine($"<p><a href=\"{Encode(Analysis.FirstStep.Resource)}\">Open suggested resource search</a></p>\n");
            }

            builder.AppendLine("</div>\n</section>\n");
        }

        if (Analysis?.Strengths?.Count > 0)
        {
            builder.AppendLine("<section class=\"section\"><h2>What you're already good at</h2>\n<ul class=\"list-card\">\n");
            foreach (var strength in Analysis.Strengths)
            {
                builder.AppendLine($"<li>{Encode(strength)}</li>\n");
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
                    builder.AppendLine($"<li><strong>{Encode(item.Skill)}</strong>: {Encode(item.Reason)}</li>\n");
                }
                builder.AppendLine("</ul>\n");
            }
            if (Analysis.MissingSkills.GoodToHave?.Count > 0)
            {
                builder.AppendLine("<h3>Good-to-Have</h3>\n<ul class=\"list-card\">\n");
                foreach (var item in Analysis.MissingSkills.GoodToHave)
                {
                    builder.AppendLine($"<li><strong>{Encode(item.Skill)}</strong>: {Encode(item.Reason)}</li>\n");
                }
                builder.AppendLine("</ul>\n");
            }
            builder.AppendLine("</section>\n");
        }

        if (Analysis?.ActionPlan?.Count > 0)
        {
            builder.AppendLine("<section class=\"section\"><h2>Action Plan</h2>\n<p class=\"section-note\">Follow this sequence to close key gaps.</p>\n<ul class=\"list-card\">\n");
            foreach (var (step, ordinal) in Analysis.ActionPlan.Select((s, i) => (s, i + 1)))
            {
                var displayStep = step.Step >= 1 ? step.Step : ordinal;
                builder.AppendLine($"<li><strong>Step {displayStep}:</strong> {Encode(step.Task)}<br>"
                    + $"<span class=\"badge\">Difficulty: {Encode(step.Difficulty)}</span>"
                    + $"<span class=\"badge\">Time: {Encode(step.Time)}</span><br>");
                if (!string.IsNullOrWhiteSpace(step.Why))
                {
                    builder.AppendLine($"<strong>Why:</strong> {Encode(step.Why)}<br>");
                }

                if (!string.IsNullOrWhiteSpace(step.SuccessCriteria))
                {
                    builder.AppendLine($"<strong>Success criteria:</strong> {Encode(step.SuccessCriteria)}<br>");
                }

                if (!string.IsNullOrWhiteSpace(step.Goal) && string.IsNullOrWhiteSpace(step.SuccessCriteria))
                {
                    builder.AppendLine($"<span class=\"badge\">Goal: {Encode(step.Goal)}</span><br>");
                }

                builder.AppendLine("</li>\n");
            }
            builder.AppendLine("</ul>\n</section>\n");
        }
        else if (Analysis?.Actions?.Count > 0)
        {
            builder.AppendLine("<section class=\"section\"><h2>Action Plan</h2>\n<p class=\"section-note\">Follow this sequence to close key gaps.</p>\n<ul class=\"list-card\">\n");
            foreach (var (action, index) in Analysis.Actions.Select((a, i) => (a, i + 1)))
            {
                builder.AppendLine($"<li><strong>Step {index}:</strong> {Encode(action.Task)}<br>"
                    + $"<span class=\"badge\">Difficulty: {Encode(action.Difficulty)}</span>"
                    + $"<span class=\"badge\">Time: {Encode(action.Time)}</span><br>");
                if (!string.IsNullOrWhiteSpace(action.Why))
                {
                    builder.AppendLine($"<strong>Why:</strong> {Encode(action.Why)}<br>");
                }

                if (!string.IsNullOrWhiteSpace(action.SuccessCriteria))
                {
                    builder.AppendLine($"<strong>Success criteria:</strong> {Encode(action.SuccessCriteria)}<br>");
                }

                builder.AppendLine("</li>\n");
            }
            builder.AppendLine("</ul>\n</section>\n");
        }

        builder.AppendLine("</div>\n</body>\n</html>");
        return builder.ToString();
    }

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

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
            >= 85 => "You're in strong shape—polish one or two gaps below and rehearse concise stories.",
            >= 65 => "You're close to interview-ready. Focus on 1–2 key areas to improve your chances.",
            >= 50 => "You're headed toward interview-ready—execute the Start Here task, then tackle the next action steps.",
            _ => "Prioritize fundamentals from this report; small shipped artifacts beat passive studying."
        };
    }

    public string DifficultySlug(string? difficulty)
    {
        var d = (difficulty ?? string.Empty).Trim().ToLowerInvariant();
        if (d.Contains("easy", StringComparison.Ordinal)) return "easy";
        if (d.Contains("hard", StringComparison.Ordinal)) return "hard";
        return "medium";
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
