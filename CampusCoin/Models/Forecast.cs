namespace CampusCoin.Models
{
    public class Forecast
    {
        public int ForecastId { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        public DateTime ForMonth { get; set; }
        public decimal? PredictedIncome { get; set; }
        public decimal? PredictedExpense { get; set; }
        public string? ConfidenceNote { get; set; }
        public DateTime GeneratedAt { get; set; }
    }
}
