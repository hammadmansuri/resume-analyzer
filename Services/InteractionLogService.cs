using System.Text.Json;

namespace resume_analyzer.Services;

public sealed class InteractionLogService
{
    private const string LogFilePath = "interaction_logs.jsonl";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly object _lock = new();
    private readonly ILogger<InteractionLogService> _logger;

    public InteractionLogService(ILogger<InteractionLogService> logger)
    {
        _logger = logger;
    }

    public void Log(string eventName, HttpContext context, IReadOnlyDictionary<string, object?>? details = null, string? sessionId = null)
    {
        var entry = new InteractionLogEntry
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            EventName = eventName,
            SessionId = CleanSessionId(sessionId),
            Path = context.Request.Path.Value ?? string.Empty,
            Method = context.Request.Method,
            UserAgent = context.Request.Headers.UserAgent.ToString(),
            Referrer = context.Request.Headers.Referer.ToString(),
            RemoteIpHash = HashForGrouping(context.Connection.RemoteIpAddress?.ToString()),
            Details = details ?? new Dictionary<string, object?>()
        };

        _logger.LogInformation(
            "Interaction {EventName} session={SessionId} path={Path} details={Details}",
            entry.EventName,
            entry.SessionId,
            entry.Path,
            JsonSerializer.Serialize(entry.Details, JsonOptions));

        try
        {
            var line = JsonSerializer.Serialize(entry, JsonOptions);
            lock (_lock)
            {
                File.AppendAllText(LogFilePath, line + Environment.NewLine);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write interaction log entry.");
        }
    }

    private static string CleanSessionId(string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return "unknown";
        }

        var cleaned = new string(sessionId.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "unknown" : cleaned[..Math.Min(cleaned.Length, 80)];
    }

    private static string HashForGrouping(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        var hash = value.GetHashCode(StringComparison.Ordinal).ToString("X");
        return hash;
    }

    private sealed class InteractionLogEntry
    {
        public DateTimeOffset TimestampUtc { get; set; }
        public string EventName { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
        public string Referrer { get; set; } = string.Empty;
        public string RemoteIpHash { get; set; } = string.Empty;
        public IReadOnlyDictionary<string, object?> Details { get; set; } = new Dictionary<string, object?>();
    }
}

public sealed class InteractionLogRequest
{
    public string EventName { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public Dictionary<string, JsonElement>? Details { get; set; }
}
