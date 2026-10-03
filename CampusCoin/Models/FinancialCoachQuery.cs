namespace CampusCoin.Models
{
    public class FinancialCoachQuery
    {
        public int QueryId { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        public string Question { get; set; } = string.Empty;
        public string AnswerText { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
