namespace CampusCoin.Models
{
    public class SavingTip
    {
        public int TipId { get; set; }
        public string TipText { get; set; } = string.Empty;
        public string? TriggerCategory { get; set; }
        public string? TriggerType { get; set; } // PercentAbove, OverBudget, SavingsBelow
        public decimal? TriggerValue { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
    }
}
