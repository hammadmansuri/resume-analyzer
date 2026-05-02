using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
            JsonTokenType.String when int.TryParse(reader.GetString(), out var value) => value,
            JsonTokenType.String => int.TryParse(reader.GetString()?.Split(' ')[0], out var value) ? value : 0,
            _ => 0
        };
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
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

    public OpenAiClient(HttpClient httpClient, AnalysisCacheService cacheService, UsageTrackingService usageService)
    {
        _httpClient = httpClient;
        _cacheService = cacheService;
        _usageService = usageService;
    }

    public async Task<ResumeAnalysisResponse> AnalyzeResumeAsync(ResumeAnalysisRequest request, string apiKey, bool useSimplifiedAnalysis = false)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("OPENAI_API_KEY must be provided.");
        }

        // Check cache first
        var inputHash = AnalysisCacheService.GenerateInputHash(request.ResumeText, request.TargetRole);
        if (_cacheService.TryGetCachedResult(inputHash, out var cachedResult))
        {
            return cachedResult!;
        }

        // If simplified analysis requested, return basic response
        if (useSimplifiedAnalysis)
        {
            var simplifiedResponse = CreateSimplifiedResponse(request.TargetRole);
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
                    content = "You are a strict senior hiring manager evaluating a candidate for a specific role. Be critical and realistic. Do NOT inflate scores. Most candidates score 55–75; scores above 80 should be rare and justified. Focus on practical, commonly required skills, not advanced or niche tooling. Use evidence-based reasoning. First extract all candidate skills from the resume and categorize them by Backend, Frontend, Cloud, DevOps, Security, Data, Testing, and Other. Then compare those skills with the market expectations. Only mark a skill as missing if it is absent or weakly demonstrated. For each missing skill, include a brief reason why it is missing or insufficient. Provide an action plan with sequenced steps. Return only valid JSON with keys: score (0-100), strengths (array of 3-4 concise strengths), missingSkills (object with mustHave and goodToHave arrays of objects containing skill and reason), actionPlan (array of objects with step, task, goal, difficulty, time), roleAssessment (object with fit, suggestedRole, confidence, reason), firstStep (string). Use 'well-matched', 'under-qualified', or 'over-qualified' for fit. For time, use a string such as '2 days' or '2-3 days'. Do not include any extra text outside the JSON object."
                },
                new
                {
                    role = "user",
                    content = $"Target Role: {request.TargetRole}\n\nMarket Expectations: {roleExpectations}\n\nResume:\n{request.ResumeText}\n\nRespond with JSON only."
                }
            }
        };

        var body = JsonSerializer.Serialize(payload);
        message.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(message);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"OpenAI request failed ({response.StatusCode}): {responseBody}");
        }

        var result = ParseOpenAiResponse(responseBody);

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
                    content = "You are a hiring expert. Given a target role, list the most important 6–8 skills currently expected in the job market. Keep it concise and practical. Return ONLY a comma-separated list of skills, nothing else."
                },
                new
                {
                    role = "user",
                    content = $"Role: {targetRole}"
                }
            }
        };

        var body = JsonSerializer.Serialize(payload);
        message.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(message);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"OpenAI request failed ({response.StatusCode}): {responseBody}");
        }

        // Parse the response and extract the skill list
        using var document = JsonDocument.Parse(responseBody);
        var choice = document.RootElement.GetProperty("choices")[0];
        var content = choice.GetProperty("message").GetProperty("content").GetString() ?? string.Empty;

        return content.Trim();
    }

    private static ResumeAnalysisResponse ParseOpenAiResponse(string json)
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
            // Log and provide fallback response with basic score
            Console.Error.WriteLine($"JSON parsing error: {ex.Message}. Providing fallback response.");
            return CreateFallbackResponse();
        }
        catch (Exception ex)
        {
            // Log any other error
            Console.Error.WriteLine($"Unexpected error parsing OpenAI response: {ex.Message}");
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
            ActionPlan = new[] { new ActionPlanItem { Step = 1, Task = "Review resume for role requirements", Goal = "Ensure resume matches job expectations", Difficulty = "Medium", Time = "1-2 hours" } },
            Actions = Array.Empty<ActionItem>(),
            Strengths = new[] { "Resume submitted successfully" },
            FirstStep = "Focus on core skills for the " + targetRole + " role.",
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
            FirstStep = "Please try again. If the issue persists, contact support.",
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

        return trimmed;
    }
}
