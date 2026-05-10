using System.Collections.Generic;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using resume_analyzer.Models;

namespace resume_analyzer.Services;

/// <summary>
/// Custom converter for string fields that might come as boolean or other types from OpenAI.
/// Gracefully converts any JSON type to string representation.
/// </summary>
public class FlexibleStringConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString() ?? string.Empty,
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            JsonTokenType.Number => reader.GetDouble().ToString(),
            JsonTokenType.Null => string.Empty,
            _ => reader.GetString() ?? string.Empty
        };
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}

public class FlexibleIntConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetInt32(),
            JsonTokenType.String => ParseFlexibleInt(reader.GetString()),
            _ => 0
        };
    }

    /// <summary>Parses integers from numeric strings or embedded numbers (e.g. "Step 1" → 1).</summary>
    private static int ParseFlexibleInt(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0;
        }

        var t = raw.Trim();
        if (int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var direct))
        {
            return direct;
        }

        var m = Regex.Match(t, @"\d+");
        return m.Success && int.TryParse(m.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var embedded)
            ? embedded
            : 0;
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}

public class FlexibleStringOrArrayConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString() ?? string.Empty,
            JsonTokenType.StartArray => ReadArray(ref reader),
            JsonTokenType.Null => string.Empty,
            _ => reader.GetString() ?? string.Empty
        };
    }

    private static string ReadArray(ref Utf8JsonReader reader)
    {
        var values = new List<string>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                break;
            }

            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    values.Add(reader.GetString() ?? string.Empty);
                    break;
                case JsonTokenType.Number:
                    values.Add(reader.GetDouble().ToString(CultureInfo.InvariantCulture));
                    break;
                case JsonTokenType.True:
                case JsonTokenType.False:
                    values.Add(reader.GetBoolean().ToString());
                    break;
                case JsonTokenType.StartObject:
                case JsonTokenType.StartArray:
                    {
                        using var document = JsonDocument.ParseValue(ref reader);
                        values.Add(document.RootElement.ToString());
                    }
                    break;
                case JsonTokenType.Null:
                    break;
                default:
                    var defaultString = reader.GetString();
                    if (!string.IsNullOrEmpty(defaultString))
                    {
                        values.Add(defaultString);
                    }
                    break;
            }
        }

        return string.Join("\n", values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()));
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}

public class FlexibleStringListConverter : JsonConverter<IReadOnlyList<string>>
{
    public override IReadOnlyList<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var items = new List<string>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    break;
                }

                if (reader.TokenType == JsonTokenType.String)
                {
                    items.Add(reader.GetString() ?? string.Empty);
                    continue;
                }

                if (reader.TokenType == JsonTokenType.Number)
                {
                    items.Add(reader.GetDouble().ToString(CultureInfo.InvariantCulture));
                    continue;
                }

                if (reader.TokenType == JsonTokenType.True || reader.TokenType == JsonTokenType.False)
                {
                    items.Add(reader.GetBoolean().ToString());
                    continue;
                }

                if (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray)
                {
                    using var document = JsonDocument.ParseValue(ref reader);
                    items.Add(document.RootElement.ToString());
                }
            }

            return items.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i.Trim()).ToArray();
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var raw = reader.GetString() ?? string.Empty;
            return raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                      .Select(x => x.Trim())
                      .Where(x => !string.IsNullOrWhiteSpace(x))
                      .ToArray();
        }

        return Array.Empty<string>();
    }

    public override void Write(Utf8JsonWriter writer, IReadOnlyList<string> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value)
        {
            writer.WriteStringValue(item);
        }
        writer.WriteEndArray();
    }
}

public sealed class OpenAiClient
{
    private const string DefaultApiUrl = "https://api.openai.com/v1/chat/completions";
    private const string DefaultModel = "gpt-4.1-mini";
    private static readonly IReadOnlyDictionary<string, string> RoleExpectations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { ".NET Developer", "C#, ASP.NET Core, Web APIs, Microservices, Docker, Azure, SQL" },
        { "Java Developer", "Java, Spring Boot, REST APIs, Microservices, Docker, AWS, SQL" },
        { "Frontend Developer", "JavaScript, React, HTML, CSS, State Management, API integration" },
        { "Full Stack Developer", "Frontend + Backend + APIs + Deployment + Databases" }
    };
    private readonly HttpClient _httpClient;
    private readonly AnalysisCacheService _cacheService;
    private readonly UsageTrackingService _usageService;
    private readonly Microsoft.Extensions.Logging.ILogger<OpenAiClient> _logger;

    public OpenAiClient(HttpClient httpClient, AnalysisCacheService cacheService, UsageTrackingService usageService, Microsoft.Extensions.Logging.ILogger<OpenAiClient> logger)
    {
        _httpClient = httpClient;
        _cacheService = cacheService;
        _usageService = usageService;
        _logger = logger;
    }

    public async Task<ResumeAnalysisResponse> AnalyzeResumeAsync(ResumeAnalysisRequest request, string apiKey, bool useSimplifiedAnalysis = false)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("OPENAI_API_KEY must be provided.");
        }

        // Check cache first
        var inputHash = AnalysisCacheService.GenerateInputHash(request.ResumeText, request.TargetRole);
        if (_cacheService.TryGetCachedResult(inputHash, out var cachedResult) && cachedResult is not null)
        {
            ResumeAnalyzerService.EnsureFirstStep(cachedResult);
            return cachedResult;
        }

        // If simplified analysis requested, return basic response
        if (useSimplifiedAnalysis)
        {
            var simplifiedResponse = CreateSimplifiedResponse(request.TargetRole);
            ResumeAnalyzerService.EnsureFirstStep(simplifiedResponse);
            _cacheService.CacheResult(inputHash, simplifiedResponse); // Cache it too
            return simplifiedResponse;
        }

        // Get or generate role expectations
        var roleExpectations = RoleExpectations.TryGetValue(request.TargetRole, out var expectations)
            ? expectations
            : await GenerateRoleExpectationsAsync(request.TargetRole, apiKey);

        using var message = new HttpRequestMessage(HttpMethod.Post, DefaultApiUrl);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var payload = new
        {
            model = DefaultModel,
            temperature = 0.2,
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = OpenAiPrompts.AnalyzeResumeSystem
                },
                new
                {
                    role = "user",
                    content = OpenAiPrompts.AnalyzeResumeUser(request.TargetRole, roleExpectations, request.ResumeText)
                }
            }
        };

        var body = JsonSerializer.Serialize(payload);
        message.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(message);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("OpenAI analysis request failed with status {StatusCode}. Raw response body: {ResponseBody}", response.StatusCode, responseBody);
            throw new HttpRequestException($"OpenAI request failed ({response.StatusCode}): {responseBody}");
        }

        var result = ParseOpenAiResponse(responseBody);
        ResumeAnalyzerService.EnsureFirstStep(result);

        // Cache the result
        _cacheService.CacheResult(inputHash, result);

        // Log usage (rough estimate: 1 token = $0.000002)
        var estimatedTokens = (request.ResumeText.Length + request.TargetRole.Length + roleExpectations.Length) / 4;
        var estimatedCost = estimatedTokens * 0.000002m;
        _usageService.LogUsage(estimatedTokens, estimatedCost);

        return result;
    }

    private async Task<string> GenerateRoleExpectationsAsync(string targetRole, string apiKey)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, DefaultApiUrl);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var payload = new
        {
            model = DefaultModel,
            temperature = 0.3,
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = OpenAiPrompts.RoleExpectationsSystem
                },
                new
                {
                    role = "user",
                    content = OpenAiPrompts.RoleExpectationsUser(targetRole)
                }
            }
        };

        var body = JsonSerializer.Serialize(payload);
        message.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(message);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("OpenAI role expectations request failed with status {StatusCode}. Raw response body: {ResponseBody}", response.StatusCode, responseBody);
            throw new HttpRequestException($"OpenAI request failed ({response.StatusCode}): {responseBody}");
        }

        // Parse the response and extract the skill list
        using var document = JsonDocument.Parse(responseBody);
        var choice = document.RootElement.GetProperty("choices")[0];
        var content = choice.GetProperty("message").GetProperty("content").GetString() ?? string.Empty;

        return content.Trim();
    }

    private ResumeAnalysisResponse ParseOpenAiResponse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var choice = document.RootElement.GetProperty("choices")[0];
            var message = choice.GetProperty("message").GetProperty("content").GetString() ?? string.Empty;

            var jsonText = ExtractJson(message);
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = false
            };

            var result = JsonSerializer.Deserialize<ResumeAnalysisResponse>(jsonText, options);
            if (result is null)
            {
                throw new InvalidOperationException("Unable to parse OpenAI response.");
            }

            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "OpenAI JSON parsing error. Raw response: {RawResponse}", json);
            return CreateFallbackResponse();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error parsing OpenAI response. Raw response: {RawResponse}", json);
            throw;
        }
    }

    private static ResumeAnalysisResponse CreateSimplifiedResponse(string targetRole)
    {
        return new ResumeAnalysisResponse
        {
            Score = 50, // Neutral score
            MissingSkills = new MissingSkills
            {
                MustHave = new[] { new MissingSkillItem { Skill = "Analysis temporarily simplified", Reason = "Due to high usage, providing basic assessment only." } },
                GoodToHave = Array.Empty<MissingSkillItem>()
            },
            ActionPlan = new[]
            {
                new ActionPlanItem
                {
                    Step = 1,
                    Task = "Rewrite three resume bullets into STAR stories with measurable outcomes for one flagship project",
                    Goal = string.Empty,
                    Why = "Screeners reward proof of impact more than skill laundry lists",
                    SuccessCriteria = "Each bullet states situation, action, and quantified or observable result",
                    Difficulty = "Easy",
                    Time = "2 days"
                }
            },
            Actions = Array.Empty<ActionItem>(),
            Strengths = new[] { "Resume submitted successfully" },
            RoleAssessment = new RoleAssessment
            {
                Fit = "under-qualified", // Conservative
                SuggestedRole = targetRole,
                Reason = "Simplified analysis - full assessment recommended when limits reset."
            }
        };
    }

    private static ResumeAnalysisResponse CreateFallbackResponse()
    {
        return new ResumeAnalysisResponse
        {
            Score = 0,
            MissingSkills = new MissingSkills
            {
                MustHave = new[] { new MissingSkillItem { Skill = "Unable to parse assessment", Reason = "The analyzer failed to interpret the AI response." } },
                GoodToHave = Array.Empty<MissingSkillItem>()
            },
            ActionPlan = Array.Empty<ActionPlanItem>(),
            Actions = Array.Empty<ActionItem>(),
            Strengths = Array.Empty<string>(),
            RoleAssessment = new RoleAssessment
            {
                Fit = "unable-to-assess",
                SuggestedRole = string.Empty,
                Reason = "JSON parsing error occurred during assessment."
            }
        };
    }

    private static string ExtractJson(string text)
    {
        var trimmed = text.Trim();

        if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            var endFenceIndex = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (endFenceIndex > 0)
            {
                trimmed = trimmed[6..endFenceIndex].Trim();
            }
        }

        if (trimmed.StartsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            var endFenceIndex = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (endFenceIndex > 0)
            {
                trimmed = trimmed[3..endFenceIndex].Trim();
            }
        }

        if (trimmed.StartsWith("{"))
        {
            return trimmed;
        }

        var jsonStart = trimmed.IndexOf('{');
        if (jsonStart < 0)
        {
            return trimmed;
        }

        var jsonEnd = FindMatchingBrace(trimmed, jsonStart);
        return jsonEnd > jsonStart ? trimmed[jsonStart..(jsonEnd + 1)].Trim() : trimmed;
    }

    private static int FindMatchingBrace(string text, int startIndex)
    {
        var depth = 0;
        var inString = false;
        var escape = false;

        for (var i = startIndex; i < text.Length; i++)
        {
            var c = text[i];

            if (escape)
            {
                escape = false;
                continue;
            }

            if (c == '\\' && inString)
            {
                escape = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
            {
                continue;
            }

            if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }
}
