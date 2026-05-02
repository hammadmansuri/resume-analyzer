using System.Collections.Concurrent;
using System.Text.Json;

namespace resume_analyzer.Services;

public class UsageTrackingService
{
    private readonly ConcurrentDictionary<string, UsageRecord> _dailyUsage = new();
    private readonly ConcurrentDictionary<string, UsageRecord> _monthlyUsage = new();
    private const string UsageFilePath = "usage_tracking.json";

    public void LogUsage(int tokensUsed, decimal estimatedCost)
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var thisMonth = DateTime.UtcNow.ToString("yyyy-MM");

        _dailyUsage.AddOrUpdate(today,
            new UsageRecord { Tokens = tokensUsed, Cost = estimatedCost, Requests = 1 },
            (key, existing) => new UsageRecord
            {
                Tokens = existing.Tokens + tokensUsed,
                Cost = existing.Cost + estimatedCost,
                Requests = existing.Requests + 1
            });

        _monthlyUsage.AddOrUpdate(thisMonth,
            new UsageRecord { Tokens = tokensUsed, Cost = estimatedCost, Requests = 1 },
            (key, existing) => new UsageRecord
            {
                Tokens = existing.Tokens + tokensUsed,
                Cost = existing.Cost + estimatedCost,
                Requests = existing.Requests + 1
            });

        // Persist to file (simple implementation)
        SaveUsageToFile();
    }

    public UsageRecord GetDailyUsage(string date) => _dailyUsage.GetValueOrDefault(date, new UsageRecord());
    public UsageRecord GetMonthlyUsage(string month) => _monthlyUsage.GetValueOrDefault(month, new UsageRecord());

    public IEnumerable<KeyValuePair<string, UsageRecord>> GetAllDailyUsage() => _dailyUsage;
    public IEnumerable<KeyValuePair<string, UsageRecord>> GetAllMonthlyUsage() => _monthlyUsage;

    private void SaveUsageToFile()
    {
        try
        {
            var data = new
            {
                Daily = _dailyUsage,
                Monthly = _monthlyUsage
            };
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(UsageFilePath, json);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to save usage data: {ex.Message}");
        }
    }

    public void LoadUsageFromFile()
    {
        try
        {
            if (File.Exists(UsageFilePath))
            {
                var json = File.ReadAllText(UsageFilePath);
                var data = JsonSerializer.Deserialize<UsageData>(json);
                if (data != null)
                {
                    foreach (var kvp in data.Daily ?? new Dictionary<string, UsageRecord>())
                        _dailyUsage[kvp.Key] = kvp.Value;
                    foreach (var kvp in data.Monthly ?? new Dictionary<string, UsageRecord>())
                        _monthlyUsage[kvp.Key] = kvp.Value;
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to load usage data: {ex.Message}");
        }
    }

    public class UsageRecord
    {
        public int Tokens { get; set; }
        public decimal Cost { get; set; }
        public int Requests { get; set; }
    }

    private class UsageData
    {
        public Dictionary<string, UsageRecord>? Daily { get; set; }
        public Dictionary<string, UsageRecord>? Monthly { get; set; }
    }
}