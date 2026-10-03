using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CampusCoin.Models
{
    /// <summary>Key/value store for single-block homepage text (hero, newsletter, contact, etc.).</summary>
    public class SiteContent
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(80)]
        [Column("ContentKey")]
        public string ContentKey { get; set; } = string.Empty;

        [Required]
        public string Value { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Label { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class HomepageTestimonial
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(600)]
        public string Quote { get; set; } = string.Empty;

        [Required, MaxLength(80)]
        public string AuthorName { get; set; } = string.Empty;

        [MaxLength(120)]
        public string AuthorMeta { get; set; } = string.Empty;

        [MaxLength(8)]
        public string AvatarInitials { get; set; } = "ST";

        [MaxLength(80)]
        public string? MetricLabel { get; set; }

        [MaxLength(40)]
        public string? MetricValue { get; set; }

        [MaxLength(40)]
        public string? MetricBadge { get; set; }

        [MaxLength(120)]
        public string? Tags { get; set; }

        [MaxLength(20)]
        public string Tone { get; set; } = "mint";

        public bool IsFeatured { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class HomepageFeature
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(120)]
        public string Title { get; set; } = string.Empty;

        [Required, MaxLength(400)]
        public string Description { get; set; } = string.Empty;

        [MaxLength(60)]
        public string Icon { get; set; } = "ri-sparkling-2-line";

        [MaxLength(40)]
        public string Status { get; set; } = "Live";

        [MaxLength(20)]
        public string Tone { get; set; } = "violet";

        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class HomepageCategoryItem
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Income or Expense</summary>
        [Required, MaxLength(20)]
        [Column("Group")]
        public string Group { get; set; } = "Expense";

        [Required, MaxLength(80)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(60)]
        public string Icon { get; set; } = "ri-price-tag-3-line";

        [Column(TypeName = "decimal(12,2)")]
        public decimal Amount { get; set; }

        /// <summary>Bar fill 0-100</summary>
        [Column("Percent")]
        public int Percent { get; set; }

        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class HomepageProof
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(80)]
        public string Label { get; set; } = string.Empty;

        [Required, MaxLength(40)]
        public string Value { get; set; } = string.Empty;

        [MaxLength(40)]
        public string Badge { get; set; } = "Verified";

        [MaxLength(20)]
        public string Tone { get; set; } = "mint";

        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class HomepageViewModel
    {
        public Dictionary<string, string> Content { get; set; } = new();
        public List<HomepageTestimonial> Testimonials { get; set; } = new();
        public List<HomepageFeature> Features { get; set; } = new();
        public List<HomepageCategoryItem> IncomeItems { get; set; } = new();
        public List<HomepageCategoryItem> ExpenseItems { get; set; } = new();
        public List<HomepageProof> Proofs { get; set; } = new();

        public string Get(string key, string fallback = "") =>
            Content.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : fallback;
    }
}
