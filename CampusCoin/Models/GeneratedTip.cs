namespace CampusCoin.Models
{
    public class GeneratedTip
    {
        public int GeneratedTipId { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        public int TipId { get; set; }
        public SavingTip? Tip { get; set; }
        public DateTime ForMonth { get; set; }
        public decimal? EstimatedImpact { get; set; }
        public int? RankOrder { get; set; }
        public DateTime GeneratedAt { get; set; }
    }
}
