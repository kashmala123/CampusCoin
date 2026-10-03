namespace CampusCoin.Models
{
    public class TipInteraction
    {
        public int InteractionId { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        public int TipId { get; set; }
        public SavingTip? Tip { get; set; }
        public bool IsDismissed { get; set; }
        public bool IsPinned { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
