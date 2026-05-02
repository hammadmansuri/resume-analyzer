using System.Collections.Concurrent;

namespace resume_analyzer.Services;

public class RateLimitingService
{
    private readonly ConcurrentDictionary<string, List<DateTime>> _requests = new();
    private const int MaxRequestsPerHour = 5;
    private static readonly TimeSpan Window = TimeSpan.FromHours(1);

    public bool IsAllowed(string key)
    {
        var now = DateTime.UtcNow;
        var userRequests = _requests.GetOrAdd(key, _ => new List<DateTime>());

        // Remove old requests outside the window
        userRequests.RemoveAll(r => now - r > Window);

        if (userRequests.Count >= MaxRequestsPerHour)
        {
            return false;
        }

        userRequests.Add(now);
        return true;
    }
}