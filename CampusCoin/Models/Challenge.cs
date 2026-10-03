namespace CampusCoin.Models
{
    public class Challenge
    {
        public int ChallengeId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int CreatedByAdminId { get; set; }
        public User? CreatedByAdmin { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
