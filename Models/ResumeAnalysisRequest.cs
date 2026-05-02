namespace resume_analyzer.Models;

public sealed class ResumeAnalysisRequest
{
    public string ResumeText { get; init; } = string.Empty;
    public string TargetRole { get; init; } = string.Empty;
}
