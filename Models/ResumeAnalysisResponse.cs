namespace resume_analyzer.Models;

public sealed class MissingSkills
{
    public IReadOnlyList<string> MustHave { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> GoodToHave { get; init; } = Array.Empty<string>();
}

public sealed class ActionItem
{
    [System.Text.Json.Serialization.JsonConverter(typeof(resume_analyzer.Services.FlexibleStringConverter))]
    public string Task { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonConverter(typeof(resume_analyzer.Services.FlexibleStringConverter))]
    public string Difficulty { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonConverter(typeof(resume_analyzer.Services.FlexibleStringConverter))]
    public string Time { get; init; } = string.Empty;
}

public sealed class RoleAssessment
{
    [System.Text.Json.Serialization.JsonConverter(typeof(resume_analyzer.Services.FlexibleStringConverter))]
    public string Fit { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonConverter(typeof(resume_analyzer.Services.FlexibleStringConverter))]
    public string SuggestedRole { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonConverter(typeof(resume_analyzer.Services.FlexibleStringConverter))]
    public string Reason { get; init; } = string.Empty;
}

public sealed class ResumeAnalysisResponse
{
    public int Score { get; init; }
    public MissingSkills MissingSkills { get; init; } = new();
    public IReadOnlyList<ActionItem> Actions { get; init; } = Array.Empty<ActionItem>();
    public IReadOnlyList<string> Strengths { get; init; } = Array.Empty<string>();

    [System.Text.Json.Serialization.JsonConverter(typeof(resume_analyzer.Services.FlexibleStringConverter))]
    public string FirstStep { get; init; } = string.Empty;

    public RoleAssessment RoleAssessment { get; init; } = new();
}
