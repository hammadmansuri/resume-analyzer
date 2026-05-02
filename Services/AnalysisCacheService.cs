using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using resume_analyzer.Models;

namespace resume_analyzer.Services;

public class AnalysisCacheService
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private const int MaxCacheSize = 1000; // Limit cache size
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(24);

    public bool TryGetCachedResult(string inputHash, out ResumeAnalysisResponse? result)
    {
        if (_cache.TryGetValue(inputHash, out var entry))
        {
            if (DateTime.UtcNow - entry.Timestamp < CacheExpiration)
            {
                result = entry.Result;
                return true;
            }
            else
            {
                // Remove expired entry
                _cache.TryRemove(inputHash, out _);
            }
        }

        result = null;
        return false;
    }

    public void CacheResult(string inputHash, ResumeAnalysisResponse result)
    {
        // Simple LRU-like eviction if cache is full
        if (_cache.Count >= MaxCacheSize)
        {
            var oldestKey = _cache.OrderBy(kvp => kvp.Value.Timestamp).First().Key;
            _cache.TryRemove(oldestKey, out _);
        }

        _cache[inputHash] = new CacheEntry
        {
            Result = result,
            Timestamp = DateTime.UtcNow
        };
    }

    public static string GenerateInputHash(string resumeText, string targetRole)
    {
        using var sha256 = SHA256.Create();
        var input = $"{resumeText.Trim()}_{targetRole.Trim()}";
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hashBytes);
    }

    private class CacheEntry
    {
        public ResumeAnalysisResponse Result { get; set; } = null!;
        public DateTime Timestamp { get; set; }
    }
}