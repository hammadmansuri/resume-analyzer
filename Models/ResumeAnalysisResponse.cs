using System.Text.Json.Serialization;

namespace resume_analyzer.Models;

public sealed class FirstStep
{
    [JsonConverter(typeof(resume_analyzer.Services.FlexibleStringConverter))]
    public string Task { get; set; } = string.Empty;

    [JsonConverter(typeof(resume_analyzer.Services.FlexibleStringConverter))]
    public string Time { get; set; } = "1–2 hours";

    [JsonConverter(typeof(resume_analyzer.Services.FlexibleStringConverter))]
    public string Outcome { get; set; } = string.Empty;

    [JsonConverter(typeof(resume_analyzer.Services.FlexibleStringConverter))]
    public string Resource { get; set; } = string.Empty;
}

public sealed class MissingSkillItem
{
    [System.Text.Json.Serialization.JsonConverter(typeof(resume_analyzer.Services.FlexibleStringConverter))]
    public string Skill { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonConverter(typeof(resume_analyzer.Services.FlexibleStringConverter))]
    public string Reason { get; init; } = string.Empty;
}

public sealed class MissingSkills
{
    public IReadOnlyList<MissingSkillItem> MustHave { get; init; } = Array.Empty<MissingSkillItem>();
    public IReadOnlyList<MissingSkillItem> GoodToHave { get; init; } = Array.Empty<MissingSkillItem>();
}

public sealed class ActionItem
{
    [System.Text.Json.Serialization.JsonConverter(typeof(resume_analyzer.Services.FlexibleStringConverter))]
    public string Task { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonConverter(typeof(resume_analyzer.Services.FlexibleStringConverter))]
    public string Difficulty { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonConverter(typeof(resume_analyzer.Services.FlexibleStringConverter))]
    public string Time { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonConverter(typeof(resume_analyzer.Services.FlexibleStringConverter))]
    public string Why { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonConverter(typeof(resume_analyzer.Services.FlexibleStringConverter))]
    public string SuccessCriteria { get; init; } = string.Empty;
}

public sealed class ActionPlanItem
{
    [System.Text.Json.Serialization.JsonConverter(typeof(resume_analyzer.Services.FlexibleIntConverter))]
    public int Step { get; init; }

    [System.Text.Json.Serialization.JsonConverter(typeof(resume_analyzer.Services.FlexibleStringConverter))]
    public string Task { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonConverter(typeof(resume_analyzer.Services.FlexibleStringConverter))]
    public string Goal { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonConverter(typeof(resume_analyzer.Services.FlexibleStringConverter))]
    public string Difficulty { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonConverter(typeof(resume_analyzer.Services.FlexibleStringConverter))]
    public string Time { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonConverter(typeof(resume_analyzer.Services.FlexibleStringConverter))]
    public string Why { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonConverter(typeof(resume_analyzer.Services.FlexibleStringConverter))]
    public string SuccessCriteria { get; init; } = string.Empty;
}

public sealed class RoleAssessment
{
    [System.Text.Json.Serialization.JsonConverter(typeof(resume_analyzer.Services.FlexibleStringConverter))]
    public string Fit { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonConverter(typeof(resume_analyzer.Services.FlexibleStringConverter))]
    public string SuggestedRole { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonConverter(typeof(resume_analyzer.Services.FlexibleStringConverter))]
    public string Confidence { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonConverter(typeof(resume_analyzer.Services.FlexibleStringConverter))]
    public string Reason { get; init; } = string.Empty;
}

public sealed class ResumeAnalysisResponse
{
    [System.Text.Json.Serialization.JsonConverter(typeof(resume_analyzer.Services.FlexibleIntConverter))]
    public int Score { get; init; }

    public MissingSkills MissingSkills { get; init; } = new();
    public IReadOnlyList<ActionPlanItem> ActionPlan { get; init; } = Array.Empty<ActionPlanItem>();
    public IReadOnlyList<ActionItem> Actions { get; init; } = Array.Empty<ActionItem>();
    public IReadOnlyList<string> Strengths { get; init; } = Array.Empty<string>();

    [JsonPropertyName("firstStep")]
    [JsonConverter(typeof(resume_analyzer.Services.FirstStepJsonConverter))]
    public FirstStep? FirstStep { get; set; }

    public RoleAssessment RoleAssessment { get; init; } = new();
}
