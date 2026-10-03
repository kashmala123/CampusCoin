namespace CampusCoin.Models.DTOs
{
    public class DashboardSummaryDto
    {
        public decimal TotalBalance { get; set; }
        public decimal MonthlyIncome { get; set; }
        public decimal MonthlyExpenses { get; set; }
        public int HealthScore { get; set; }
        public string HealthScoreStatus { get; set; } = "Good Health";
        public HealthBreakdownDto HealthBreakdown { get; set; } = new();
        public decimal BudgetUsagePercentage { get; set; }
        public decimal BalanceChangePercent { get; set; }
        public int UnreadNotificationsCount { get; set; }
        public string UserName { get; set; } = "Student";
        public string UserAcademicYear { get; set; } = "Undergraduate";
        public decimal AllowanceBaseline { get; set; }
    }

    public class HealthBreakdownDto
    {
        public int BudgetControlScore { get; set; } = 25; // 0-25
        public int SavingRateScore { get; set; } = 25;    // 0-25
        public int GoalProgressScore { get; set; } = 25;  // 0-25
        public int ExpenseStabilityScore { get; set; } = 25; // 0-25
    }

    public class DashboardChartsDto
    {
        public CashflowChartData Cashflow { get; set; } = new();
        public DonutChartData SpendingDonut { get; set; } = new();
        public SimpleChartData CategoryBar { get; set; } = new();
        public BudgetVsActualChartData BudgetVsActual { get; set; } = new();
        public CashflowChartData SixMonthComparison { get; set; } = new();
        public SimpleChartData WeeklyTrend { get; set; } = new();
        public SavingsRadialChartData SavingsRadial { get; set; } = new();
        public RadarChartData HealthRadar { get; set; } = new();
        public SimpleChartData TopSpending { get; set; } = new();
    }

    public class CashflowChartData
    {
        public List<string> Labels { get; set; } = new();
        public List<decimal> IncomeData { get; set; } = new();
        public List<decimal> ExpenseData { get; set; } = new();
    }

    public class DonutChartData
    {
        public List<string> Labels { get; set; } = new();
        public List<decimal> Data { get; set; } = new();
        public List<string> Colors { get; set; } = new();
    }

    public class SimpleChartData
    {
        public List<string> Labels { get; set; } = new();
        public List<decimal> Data { get; set; } = new();
        public List<string> Colors { get; set; } = new();
    }

    public class BudgetVsActualChartData
    {
        public List<string> Labels { get; set; } = new();
        public List<decimal> BudgetLimits { get; set; } = new();
        public List<decimal> ActualSpent { get; set; } = new();
    }

    public class SavingsRadialChartData
    {
        public decimal TargetAmount { get; set; }
        public decimal CurrentAmount { get; set; }
        public decimal Percentage { get; set; }
        public string GoalName { get; set; } = "Semester Buffer";
    }

    public class RadarChartData
    {
        public List<string> Labels { get; set; } = new()
        {
            "Budget Control", "Saving Rate", "Goal Progress", "Spending Stability"
        };
        public List<int> Scores { get; set; } = new() { 25, 25, 25, 25 };
    }

    public class AffordabilityRequestDto
    {
        public string ItemName { get; set; } = string.Empty;
        public decimal ItemCost { get; set; }
    }

    public class AffordabilityResponseDto
    {
        public bool IsAffordable { get; set; }
        public string Status { get; set; } = "Safe"; // Safe, Caution, Unaffordable
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal CurrentBalance { get; set; }
        public decimal PostPurchaseBalance { get; set; }
        public decimal SafeBuffer { get; set; } = 300.00m;
        public bool SafeBufferMaintained { get; set; }
    }

    public class SmartTipDto
    {
        public int TipId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Icon { get; set; } = "fa-lightbulb";
        public string Color { get; set; } = "sky"; // sky, emerald, amber, rose, purple
        public decimal? EstimatedImpact { get; set; }
        public bool IsPinned { get; set; }
        public bool IsDismissed { get; set; }
    }

    public class NotificationDto
    {
        public int NotificationId { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = "Info"; // Info, Warning, Alert
        public bool IsRead { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
    }
}
