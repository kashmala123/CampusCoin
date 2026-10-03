namespace CampusCoin.Models
{
    public class Bookmark
    {
        public int BookmarkId { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }

        /// <summary>Tip, Insight, or SmartTip</summary>
        public string RefType { get; set; } = string.Empty;
        public int RefId { get; set; }

        /// <summary>Snapshot so smart tips (hash ids) still show after refresh.</summary>
        public string? Title { get; set; }
        public string? Content { get; set; }
        public string? Category { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
