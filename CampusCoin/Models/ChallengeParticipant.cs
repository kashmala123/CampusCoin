namespace CampusCoin.Models
{
    public class ChallengeParticipant
    {
        public int ParticipantId { get; set; }
        public int ChallengeId { get; set; }
        public Challenge? Challenge { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        public bool HasCompleted { get; set; }
        public DateTime JoinedAt { get; set; }
    }
}
