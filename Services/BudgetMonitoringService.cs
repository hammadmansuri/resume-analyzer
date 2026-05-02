using System.Collections.Concurrent;

namespace resume_analyzer.Services;

public class BudgetMonitoringService
{
    private readonly UsageTrackingService _usageService;
    private const decimal MonthlyBudgetLimit = 50.0m; // Example: $50/month
    private const decimal AlertThreshold = 0.8m; // 80%
    private readonly ConcurrentDictionary<string, bool> _alertsSent = new();

    public BudgetMonitoringService(UsageTrackingService usageService)
    {
        _usageService = usageService;
    }

    public BudgetStatus CheckBudget()
    {
        var thisMonth = DateTime.UtcNow.ToString("yyyy-MM");
        var monthlyUsage = _usageService.GetMonthlyUsage(thisMonth);

        var currentSpend = monthlyUsage.Cost;
        var budgetRemaining = MonthlyBudgetLimit - currentSpend;
        var percentageUsed = MonthlyBudgetLimit > 0 ? (currentSpend / MonthlyBudgetLimit) * 100 : 0;

        var isNearLimit = percentageUsed >= (AlertThreshold * 100);
        var isOverLimit = currentSpend >= MonthlyBudgetLimit;

        // Log alert if near limit and not already alerted this month
        if (isNearLimit && !_alertsSent.ContainsKey(thisMonth))
        {
            _alertsSent[thisMonth] = true;
            Console.WriteLine($"BUDGET ALERT: Monthly budget usage at {percentageUsed:F1}% (${currentSpend:F2} of ${MonthlyBudgetLimit:F2})");
        }

        return new BudgetStatus
        {
            CurrentSpend = currentSpend,
            BudgetLimit = MonthlyBudgetLimit,
            BudgetRemaining = Math.Max(0, budgetRemaining),
            PercentageUsed = percentageUsed,
            IsNearLimit = isNearLimit,
            IsOverLimit = isOverLimit
        };
    }

    public class BudgetStatus
    {
        public decimal CurrentSpend { get; set; }
        public decimal BudgetLimit { get; set; }
        public decimal BudgetRemaining { get; set; }
        public decimal PercentageUsed { get; set; }
        public bool IsNearLimit { get; set; }
        public bool IsOverLimit { get; set; }
    }
}