using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using resume_analyzer.Models;
using System.Text.Json;

namespace resume_analyzer.Pages;

public class ResultsModel : PageModel
{
    public ResumeAnalysisResponse? Analysis { get; set; }
    public string? TargetRole { get; set; }

    public void OnGet()
    {
        if (TempData["AnalysisResult"] is string analysisJson)
        {
            try
            {
                var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    PropertyNameCaseInsensitive = true
                };
                Analysis = JsonSerializer.Deserialize<ResumeAnalysisResponse>(analysisJson, options);
                TargetRole = TempData["TargetRole"]?.ToString();
            }
            catch
            {
                // If deserialization fails, analysis will be null
            }
        }
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
