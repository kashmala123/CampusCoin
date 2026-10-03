namespace CampusCoin.Models
{
    public class Notification
    {
        public int NotificationId { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = "Info"; // Info, Warning, Alert
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
