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

    public OpenAiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ResumeAnalysisResponse> AnalyzeResumeAsync(ResumeAnalysisRequest request, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("OPENAI_API_KEY must be provided.");
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
                    content = "You are a strict senior hiring manager evaluating a candidate for a specific role. Be critical and realistic. Do NOT inflate scores. Rules: - Most candidates fall between 55–75 - Scores above 80 should be rare and justified - Avoid outdated or low-demand technologies - Focus only on skills that are currently in demand in the job market. Prioritize practical, commonly required skills over advanced or niche tools. Avoid recommending highly advanced tools (e.g., Kubernetes) unless clearly required. Focus on skills that improve employability quickly. Must-have skills should be frequently required across most job descriptions, not niche or advanced tooling. To identify missing skills: First extract and list all candidate's existing skills from the resume. Then compare with market expectations. Do NOT list a skill as missing if it is clearly present in the resume. Carefully verify before marking a skill as missing. Avoid false negatives. Evaluate if the candidate is under-leveled or over-qualified for the target role and suggest a more appropriate role if needed. Actions must: - Be achievable by an individual developer - Not require leading a team - Be completable within 1–3 weeks - Be portfolio-worthy (GitHub project, deployable work) - Small, independently completable tasks (2–5 days each), not large systems - Ensure at least one action is beginner-friendly and can be completed in 1–2 days. Return only valid JSON with keys: score (0-100), strengths (array of 3-4 key strengths), missingSkills (object with mustHave and goodToHave arrays), actions (array of objects with task, difficulty, time), roleAssessment (object with fit (string: 'well-matched', 'under-qualified', 'over-qualified'), suggestedRole (string), reason (string)), firstStep (string for very small action user can start today). For time field in actions, return as string like '2 days' or '2-3 days'. IMPORTANT: All fields must be strings in JSON, not booleans or numbers."
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

        return ParseOpenAiResponse(responseBody);
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

    private static ResumeAnalysisResponse CreateFallbackResponse()
    {
        return new ResumeAnalysisResponse
        {
            Score = 0,
            MissingSkills = new MissingSkills
            {
                MustHave = new[] { "Unable to parse assessment" }.AsReadOnly(),
                GoodToHave = Array.Empty<string>()
            },
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
