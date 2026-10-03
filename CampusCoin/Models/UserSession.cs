namespace CampusCoin.Models
{
    public class UserSession
    {
        public int SessionId { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        public string SessionToken { get; set; } = string.Empty;
        public string? IpAddress { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsRevoked { get; set; }
    }
}
