using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using resume_analyzer.Models;

namespace resume_analyzer.Services;

/// <summary>
/// Accepts legacy API/cache payloads where <c>firstStep</c> was a string, or a structured object.
/// </summary>
public sealed class FirstStepJsonConverter : JsonConverter<FirstStep?>
{
    public override FirstStep? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.String:
                var s = reader.GetString()?.Trim() ?? "";
                return string.IsNullOrEmpty(s)
                    ? null
                    : new FirstStep { Task = s, Time = "1–2 hours", Outcome = string.Empty, Resource = string.Empty };
            case JsonTokenType.StartObject:
                return JsonSerializer.Deserialize<FirstStep>(ref reader, options);
            default:
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, FirstStep? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        JsonSerializer.Serialize(writer, value, options);
    }
}

/// <summary>
/// Builds the actionable first step from analysis results (easiest planned task first).
/// </summary>
public static class ResumeAnalyzerService
{
    private const string StandardFirstStepDuration = "1–2 hours";

    private static readonly Regex VagueTaskLanguage = new(
        @"\b(learn|learning|understand|understanding|improve|improving|deepen|stud(y|ying)|explore|exploring|enhance|enhancing|practice\s+your|get\s+better\s+at|gain\s+knowledge|brush\s+up\s+on)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private sealed record PickedStep(string Task, string Goal, string Why, string SuccessCriteria);

    public static void EnsureFirstStep(ResumeAnalysisResponse response)
    {
        var picked = PickEasiestStep(response);

        string task;
        string outcome;

        if (picked != null)
        {
            task = RefineFirstStepTask(picked.Task, response);
            outcome = BuildConcreteOutcome(picked.SuccessCriteria, picked.Why, picked.Goal, task);
        }
        else if (response.FirstStep is { Task: var existingTask } && !string.IsNullOrWhiteSpace(existingTask))
        {
            task = RefineFirstStepTask(existingTask.Trim(), response);
            outcome = string.IsNullOrWhiteSpace(response.FirstStep.Outcome)
                ? DefaultOutcome(task)
                : TrimLength(response.FirstStep.Outcome.Trim(), 220);
        }
        else
        {
            task = BuildFallbackConcreteTask(response);
            outcome = "You will have one stronger bullet that proves impact.";
        }

        response.FirstStep = new FirstStep
        {
            Task = task,
            Time = StandardFirstStepDuration,
            Outcome = outcome,
            Resource = BuildGoogleSearchUrl(task)
        };

        Console.WriteLine($"FIRST_STEP_GENERATED: {task}");
    }

    private static PickedStep? PickEasiestStep(ResumeAnalysisResponse response)
    {
        if (response.ActionPlan?.Count > 0)
        {
            var best = response.ActionPlan
                .OrderBy(a => DifficultyRank(a.Difficulty))
                .ThenBy(a => a.Step)
                .First();
            return new PickedStep(best.Task, best.Goal, best.Why, best.SuccessCriteria);
        }

        if (response.Actions?.Count > 0)
        {
            var best = response.Actions
                .Select((a, index) => (a, index))
                .OrderBy(x => DifficultyRank(x.a.Difficulty))
                .ThenBy(x => x.index)
                .First();
            return new PickedStep(best.a.Task, string.Empty, best.a.Why, best.a.SuccessCriteria);
        }

        return null;
    }

    private static string RefineFirstStepTask(string task, ResumeAnalysisResponse response)
    {
        var t = task.Trim();
        if (string.IsNullOrEmpty(t))
        {
            return BuildFallbackConcreteTask(response);
        }

        if (VagueTaskLanguage.IsMatch(t))
        {
            return BuildFallbackConcreteTask(response);
        }

        return t;
    }

    private static string BuildFallbackConcreteTask(ResumeAnalysisResponse response)
    {
        var skill = response.MissingSkills?.MustHave?.FirstOrDefault()?.Skill?.Trim();
        if (string.IsNullOrEmpty(skill))
        {
            skill = response.MissingSkills?.GoodToHave?.FirstOrDefault()?.Skill?.Trim();
        }

        if (string.IsNullOrEmpty(skill))
        {
            return "Write three measurable resume bullets using STAR (situation, task, action, result) for one project you can demo.";
        }

        return $"Design or implement one small artifact that proves '{skill}'—choose a runnable mini-demo, API sketch with sample requests, or a labeled diagram plus a short README describing tradeoffs.";
    }

    private static string BuildConcreteOutcome(string successCriteria, string why, string goal, string task)
    {
        var sc = successCriteria.Trim();
        if (!string.IsNullOrEmpty(sc))
        {
            return $"Done means: {TrimLength(sc, 200)}";
        }

        var w = why.Trim();
        if (!string.IsNullOrEmpty(w))
        {
            return $"{TrimLength(w, 160)} Deliver something you can screenshot, commit, or link.";
        }

        var g = goal.Trim();
        if (!string.IsNullOrEmpty(g))
        {
            return $"You'll ship progress toward: {TrimLength(g, 160)}";
        }

        return DefaultOutcome(task);
    }

    private static string DefaultOutcome(string task)
    {
        return $"You'll finish with a tangible artifact tied to: {TrimLength(task.Trim(), 120)}";
    }

    private static string TrimLength(string value, int max)
    {
        var v = value.Trim();
        if (v.Length <= max)
        {
            return v;
        }

        return v[..(max - 1)].TrimEnd() + "…";
    }

    private static int DifficultyRank(string difficulty)
    {
        var d = difficulty.Trim().ToLowerInvariant();
        if (d.Contains("easy", StringComparison.Ordinal)) return 0;
        if (d.Contains("hard", StringComparison.Ordinal)) return 2;
        return 1;
    }

    private static string BuildGoogleSearchUrl(string query)
    {
        var q = query.Trim();
        return string.IsNullOrEmpty(q)
            ? string.Empty
            : "https://www.google.com/search?q=" + Uri.EscapeDataString(q);
    }
}
