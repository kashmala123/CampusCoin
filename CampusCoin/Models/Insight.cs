namespace CampusCoin.Models
{
    public class Insight
    {
        public int InsightId { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        public DateTime Month { get; set; }
        public string? SummaryText { get; set; }
        public string? TipText { get; set; }
        public DateTime GeneratedAt { get; set; }
    }
}
