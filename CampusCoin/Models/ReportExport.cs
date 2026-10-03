namespace CampusCoin.Models
{
    public class ReportExport
    {
        public int ExportId { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        public string ReportType { get; set; } = string.Empty; // CategoryWise, IncomeVsExpense, DailySummary, WeeklySummary, SavingsSummary
        public DateTime? RangeStart { get; set; }
        public DateTime? RangeEnd { get; set; }
        public string Format { get; set; } = string.Empty; // PDF, Image
        public string? SharedWithEmail { get; set; }
        public DateTime ExportedAt { get; set; }
    }
}
