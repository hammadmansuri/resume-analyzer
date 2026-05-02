using System.Collections.Concurrent;
using System.Text.Json;

namespace resume_analyzer.Services;

public class FeedbackService
{
    private readonly ConcurrentBag<FeedbackRecord> _feedbackRecords = new();
    private const string FeedbackFilePath = "user_feedback.json";

    public void AddFeedback(FeedbackRecord feedback)
    {
        feedback.Timestamp = DateTime.UtcNow;
        _feedbackRecords.Add(feedback);
        SaveFeedbackToFile();
    }

    public IEnumerable<FeedbackRecord> GetAllFeedback() => _feedbackRecords;

    private void SaveFeedbackToFile()
    {
        try
        {
            var json = JsonSerializer.Serialize(_feedbackRecords, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FeedbackFilePath, json);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to save feedback data: {ex.Message}");
        }
    }

    public void LoadFeedbackFromFile()
    {
        try
        {
            if (File.Exists(FeedbackFilePath))
            {
                var json = File.ReadAllText(FeedbackFilePath);
                var records = DeserializeFeedbackRecords(json);
                if (records != null)
                {
                    foreach (var record in records)
                    {
                        _feedbackRecords.Add(record);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to load feedback data: {ex.Message}");
        }
    }

    private static List<FeedbackRecord>? DeserializeFeedbackRecords(string json)
    {
        using var document = JsonDocument.Parse(json);

        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            return document.RootElement.Deserialize<List<FeedbackRecord>>();
        }

        if (document.RootElement.ValueKind == JsonValueKind.Object &&
            document.RootElement.TryGetProperty("Feedback", out var feedbackElement) &&
            feedbackElement.ValueKind == JsonValueKind.Array)
        {
            return feedbackElement.Deserialize<List<FeedbackRecord>>();
        }

        return new List<FeedbackRecord>();
    }

    public class FeedbackRecord
    {
        public string? TargetRole { get; set; }
        public int Score { get; set; }
        public int Rating { get; set; } // 1-5 stars
        public string? Comments { get; set; }
        public bool Helpful { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
