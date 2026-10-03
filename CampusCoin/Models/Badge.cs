namespace CampusCoin.Models
{
    public class Badge
    {
        public int BadgeId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? IconEmoji { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
