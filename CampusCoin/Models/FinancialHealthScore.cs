namespace CampusCoin.Models
{
    public class FinancialHealthScore
    {
        public int ScoreId { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        public DateTime ForMonth { get; set; }
        public int OverallScore { get; set; }
        public int? BudgetControlScore { get; set; }
        public int? SavingRateScore { get; set; }
        public int? ExpenseStabilityScore { get; set; }
        public int? GoalProgressScore { get; set; }
        public DateTime GeneratedAt { get; set; }
    }
}
