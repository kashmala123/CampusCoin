namespace CampusCoin.Models
{
    public class SpendingPersonality
    {
        public int UserId { get; set; } // PK + FK (1:1 with Users)
        public User? User { get; set; }
        public string PersonalityLabel { get; set; } = string.Empty;
        public DateTime DeterminedAt { get; set; }
    }
}
