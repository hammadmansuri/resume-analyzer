using System.Collections.Concurrent;

namespace resume_analyzer.Services;

public class RateLimitingService
{
    private readonly ConcurrentDictionary<string, RequestWindow> _requests = new();
    private const int MaxRequestsPerHour = 5;
    private static readonly TimeSpan Window = TimeSpan.FromHours(1);

    public bool IsAllowed(string key)
    {
        var now = DateTime.UtcNow;
        var userRequests = _requests.GetOrAdd(key, _ => new RequestWindow());

        lock (userRequests.SyncRoot)
        {
            userRequests.Requests.RemoveAll(r => now - r > Window);

            if (userRequests.Requests.Count >= MaxRequestsPerHour)
            {
                return false;
            }

            userRequests.Requests.Add(now);
            return true;
        }
    }

    private sealed class RequestWindow
    {
        public object SyncRoot { get; } = new();
        public List<DateTime> Requests { get; } = new();
    }
}
