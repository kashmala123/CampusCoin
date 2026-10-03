using CampusCoin.Data;
using CampusCoin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CampusCoin.Controllers
{
    [Authorize(Roles = "Admin,DemoAdmin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;

        public AdminController(ApplicationDbContext context, IPasswordHasher<User> passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out int id) ? id : 0;
        }

        /// <summary>Cryptographically random temporary password (never a fixed default).</summary>
        private static string GenerateTemporaryPassword(int length = 12)
        {
            const string chars = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789!@#$";
            var bytes = new byte[length];
            System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
            var result = new char[length];
            for (int i = 0; i < length; i++)
                result[i] = chars[bytes[i] % chars.Length];
            return new string(result);
        }

        // ============== VIEW ROUTES ==============
        [HttpGet("/Admin")]
        [HttpGet("/Admin/Index")]
        public IActionResult Index() => View();

        [HttpGet("/Admin/Students")]
        public IActionResult Students() => View();

        [HttpGet("/Admin/Transactions")]
        public IActionResult Transactions() => View();

        [HttpGet("/Admin/Categories")]
        public IActionResult Categories() => View();

        [HttpGet("/Admin/Reports")]
        public IActionResult Reports() => View();

        [HttpGet("/Admin/Budgets")]
        public IActionResult Budgets() => View();

        [HttpGet("/Admin/SavingGoals")]
        public IActionResult SavingGoals() => View();

        [HttpGet("/Admin/Notifications")]
        public IActionResult Notifications() => View();

        [HttpGet("/Admin/Settings")]
        public IActionResult Settings() => View();

        [HttpGet("/Admin/EventManager")]
        public IActionResult EventManager() => View();

        [HttpGet("/Admin/VoucherManagement")]
        public IActionResult VoucherManagement() => View();

        [HttpGet("/Admin/SpendingAlerts")]
        public IActionResult SpendingAlerts() => View();

        [HttpGet("/Admin/Comparison")]
        public IActionResult Comparison() => View();

        [HttpGet("/Admin/HomepageCms")]
        public IActionResult HomepageCms() => View();

        // ============== HOMEPAGE CMS APIs ==============

        [HttpPost("/api/admin/cms/ensure")]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EnsureCmsSchema()
        {
            try
            {
                async Task Exec(string sql)
                {
                    try { await _context.Database.ExecuteSqlRawAsync(sql); }
                    catch (Exception ex) { Console.WriteLine("CMS SQL warn: " + ex.Message); }
                }

                await Exec(@"
IF OBJECT_ID(N'dbo.SiteContents', N'U') IS NULL
CREATE TABLE dbo.SiteContents (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ContentKey NVARCHAR(80) NOT NULL,
    Value NVARCHAR(MAX) NOT NULL,
    Label NVARCHAR(200) NULL,
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);");
                await Exec(@"
IF OBJECT_ID(N'dbo.SiteContents', N'U') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SiteContents_ContentKey' AND object_id = OBJECT_ID(N'dbo.SiteContents'))
CREATE UNIQUE INDEX IX_SiteContents_ContentKey ON dbo.SiteContents(ContentKey);");
                await Exec(@"
IF OBJECT_ID(N'dbo.HomepageTestimonials', N'U') IS NULL
CREATE TABLE dbo.HomepageTestimonials (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Quote NVARCHAR(600) NOT NULL,
    AuthorName NVARCHAR(80) NOT NULL,
    AuthorMeta NVARCHAR(120) NOT NULL DEFAULT N'',
    AvatarInitials NVARCHAR(8) NOT NULL DEFAULT N'ST',
    MetricLabel NVARCHAR(80) NULL,
    MetricValue NVARCHAR(40) NULL,
    MetricBadge NVARCHAR(40) NULL,
    Tags NVARCHAR(120) NULL,
    Tone NVARCHAR(20) NOT NULL DEFAULT N'mint',
    IsFeatured BIT NOT NULL DEFAULT 0,
    SortOrder INT NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1
);");
                await Exec(@"
IF OBJECT_ID(N'dbo.HomepageFeatures', N'U') IS NULL
CREATE TABLE dbo.HomepageFeatures (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Title NVARCHAR(120) NOT NULL,
    Description NVARCHAR(400) NOT NULL,
    Icon NVARCHAR(60) NOT NULL DEFAULT N'ri-sparkling-2-line',
    Status NVARCHAR(40) NOT NULL DEFAULT N'Live',
    Tone NVARCHAR(20) NOT NULL DEFAULT N'violet',
    SortOrder INT NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1
);");
                await Exec(@"
IF OBJECT_ID(N'dbo.HomepageCategoryItems', N'U') IS NULL
CREATE TABLE dbo.HomepageCategoryItems (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    [Group] NVARCHAR(20) NOT NULL,
    Name NVARCHAR(80) NOT NULL,
    Icon NVARCHAR(60) NOT NULL DEFAULT N'ri-price-tag-3-line',
    Amount DECIMAL(12,2) NOT NULL DEFAULT 0,
    [Percent] INT NOT NULL DEFAULT 0,
    SortOrder INT NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1
);");
                await Exec(@"
IF OBJECT_ID(N'dbo.HomepageProofs', N'U') IS NULL
CREATE TABLE dbo.HomepageProofs (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Label NVARCHAR(80) NOT NULL,
    Value NVARCHAR(40) NOT NULL,
    Badge NVARCHAR(40) NOT NULL DEFAULT N'Verified',
    Tone NVARCHAR(20) NOT NULL DEFAULT N'mint',
    SortOrder INT NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1
);");

                if (!await _context.SiteContents.AnyAsync())
                {
                    var seed = new (string K, string V, string L)[] {
                        ("hero_label","Student money, made clear","Hero label"),
                        ("hero_title","Know where your money goes.","Hero title"),
                        ("hero_subtitle","Track spending, understand your habits, and build better financial decisions.","Hero subtitle"),
                        ("hero_cta","Get Started","Hero primary button"),
                        ("hero_link","Explore all features","Hero secondary link"),
                        ("features_label","Built for student life","Features label"),
                        ("features_title","Everything you need to stay on top of campus money.","Features title"),
                        ("features_subtitle","From daily spends to semester goals.","Features subtitle"),
                        ("categories_label","Categories that fit real life","Categories label"),
                        ("categories_title","Built for how students actually spend.","Categories title"),
                        ("categories_subtitle","From canteen food to hostel rent — every category is designed around student life.","Categories subtitle"),
                        ("categories_cta_title","Manage your own categories","Categories CTA title"),
                        ("categories_cta_text","Add, edit, or delete any category — your categories, your rules.","Categories CTA text"),
                        ("categories_cta_button","Try it","Categories CTA button"),
                        ("testimonials_label","Loved by students","Testimonials label"),
                        ("testimonials_title","Real students. Real savings.","Testimonials title"),
                        ("testimonials_subtitle","Over 12,000 students use CashCampus to track allowances and build better money patterns.","Testimonials subtitle"),
                        ("newsletter_title","Money tips in your inbox","Newsletter title"),
                        ("newsletter_subtitle","Short, useful notes for campus life — no spam.","Newsletter subtitle"),
                        ("newsletter_button","Subscribe","Newsletter button"),
                        ("contact_label","Get in touch","Contact label"),
                        ("contact_title","Questions? We're here.","Contact title"),
                        ("contact_subtitle","Send a message and we'll get back within one working day.","Contact subtitle"),

                        ("show_features","1","Show features section"),
                        ("show_categories","1","Show categories section"),
                        ("show_manage_bar","1","Show manage categories bar"),
                        ("show_testimonials","1","Show testimonials section"),
                        ("show_proofs","1","Show proof chips"),
                        ("show_newsletter","1","Show newsletter section"),
                        ("show_contact","1","Show contact section"),
                        ("hero_badge","Student money, made clear","Hero badge"),
                        ("footer_tagline","Money tools built for campus life.","Footer tagline"),
                        ("ai_coach_placeholder","Ask about your spending...","AI coach placeholder"),
                        ("cta_secondary_label","Explore all features","Secondary CTA label"),

                        ("income_total","32500","Demo income total"),
                        ("expense_total","24850","Demo expense total"),
                    };
                    foreach (var (k,v,l) in seed)
                        _context.SiteContents.Add(new SiteContent { ContentKey = k, Value = v, Label = l, UpdatedAt = DateTime.UtcNow });
                    await _context.SaveChangesAsync();
                }

                
                // Ensure design keys exist even if content already seeded
                {
                    var designKeys = new (string K, string V, string L)[] {
                        ("show_features","1","Show features section"),
                        ("show_categories","1","Show categories section"),
                        ("show_manage_bar","1","Show manage categories bar"),
                        ("show_testimonials","1","Show testimonials section"),
                        ("show_proofs","1","Show proof chips"),
                        ("show_newsletter","1","Show newsletter section"),
                        ("show_contact","1","Show contact section"),
                        ("hero_badge","Student money, made clear","Hero badge"),
                        ("footer_tagline","Money tools built for campus life.","Footer tagline"),
                        ("ai_coach_placeholder","Ask about your spending...","AI coach placeholder"),
                        ("cta_secondary_label","Explore all features","Secondary CTA label"),
                        // Category card design (homepage)
                        ("cat_card_density","comfortable","Category card density: compact|comfortable|spacious"),
                        ("cat_card_radius","22","Category card border radius (px)"),
                        ("cat_card_equal_height","0","Force equal height category cards 1/0"),
                        ("income_title","Income","Income panel title"),
                        ("income_subtitle","Where your money comes from","Income panel subtitle"),
                        ("expense_title","Expenses","Expense panel title"),
                        ("expense_subtitle","Where it actually goes","Expense panel subtitle"),
                        ("income_total","32500","Demo income total"),
                        ("expense_total","24850","Demo expense total"),
                    };
                    var existing = await _context.SiteContents.Select(x => x.ContentKey).ToListAsync();
                    foreach (var (k,v,l) in designKeys)
                    {
                        if (!existing.Contains(k))
                            _context.SiteContents.Add(new SiteContent { ContentKey = k, Value = v, Label = l, UpdatedAt = DateTime.UtcNow });
                    }
                    await _context.SaveChangesAsync();
                }

                if (!await _context.HomepageFeatures.AnyAsync())
                {
                    _context.HomepageFeatures.AddRange(
                        new HomepageFeature { Title = "See your real balance", Description = "Live totals for income, expenses, and what's left — updated as you log.", Icon = "ri-wallet-3-line", Status = "Live", Tone = "violet", SortOrder = 1 },
                        new HomepageFeature { Title = "AI that speaks student", Description = "Clear, friendly insights on your habits — no jargon, just what to do next.", Icon = "ri-sparkling-2-line", Status = "Live", Tone = "mint", SortOrder = 2 },
                        new HomepageFeature { Title = "Budgets without the guilt", Description = "Soft limits and visual cues so you stay on track without stress.", Icon = "ri-pie-chart-2-line", Status = "On track", Tone = "coral", SortOrder = 3 },
                        new HomepageFeature { Title = "Reports you can share", Description = "Weekly and monthly snapshots built for allowance life on campus.", Icon = "ri-bar-chart-box-line", Status = "Ready", Tone = "amber", SortOrder = 4 }
                    );
                    await _context.SaveChangesAsync();
                }

                if (!await _context.HomepageCategoryItems.AnyAsync())
                {
                    _context.HomepageCategoryItems.AddRange(
                        new HomepageCategoryItem { Group = "Income", Name = "Allowance", Icon = "ri-hand-coin-line", Amount = 18000, Percent = 55, SortOrder = 1 },
                        new HomepageCategoryItem { Group = "Income", Name = "Part-time job", Icon = "ri-briefcase-4-line", Amount = 9500, Percent = 29, SortOrder = 2 },
                        new HomepageCategoryItem { Group = "Income", Name = "Scholarship", Icon = "ri-graduation-cap-line", Amount = 4000, Percent = 12, SortOrder = 3 },
                        new HomepageCategoryItem { Group = "Expense", Name = "Food", Icon = "ri-restaurant-line", Amount = 8420, Percent = 34, SortOrder = 1 },
                        new HomepageCategoryItem { Group = "Expense", Name = "Hostel / Rent", Icon = "ri-home-4-line", Amount = 6500, Percent = 26, SortOrder = 2 },
                        new HomepageCategoryItem { Group = "Expense", Name = "Transport", Icon = "ri-bus-line", Amount = 3180, Percent = 13, SortOrder = 3 }
                    );
                    await _context.SaveChangesAsync();
                }

                if (!await _context.HomepageTestimonials.AnyAsync())
                {
                    _context.HomepageTestimonials.Add(new HomepageTestimonial {
                        Quote = "Finally an app that understands hostel life.",
                        AuthorName = "Aarav K.", AuthorMeta = "2nd year · Engineering", AvatarInitials = "AK",
                        MetricLabel = "Saved in 2 months", MetricValue = "Rs. 3,200", MetricBadge = "Real result",
                        Tone = "mint", SortOrder = 1, IsActive = true
                    });
                    await _context.SaveChangesAsync();
                }

                if (!await _context.HomepageProofs.AnyAsync())
                {
                    _context.HomepageProofs.AddRange(
                        new HomepageProof { Label = "Saved in 2 months", Value = "Rs. 3,200", Badge = "Real result", Tone = "violet", SortOrder = 1 },
                        new HomepageProof { Label = "Cut food delivery by", Value = "40%", Badge = "Verified", Tone = "rose", SortOrder = 2 },
                        new HomepageProof { Label = "Privacy-first setup", Value = "0 bank links", Badge = "Verified", Tone = "mint", SortOrder = 3 }
                    );
                    await _context.SaveChangesAsync();
                }

                
                // Repair test/placeholder feature content if still present
                try
                {
                    var bad = await _context.HomepageFeatures
                        .Where(f => f.Title == "Friendly insight" || f.Description == "aaaaaaaaaaaa" || f.Description == "aaaaaaaaaaa")
                        .ToListAsync();
                    foreach (var f in bad)
                    {
                        f.Title = "AI that speaks student";
                        f.Description = "Clear, friendly insights on your habits — no jargon, just what to do next.";
                        f.Status = "Live";
                        f.Icon = string.IsNullOrWhiteSpace(f.Icon) ? "ri-sparkling-2-line" : f.Icon;
                    }
                    if (bad.Count > 0) await _context.SaveChangesAsync();
                }
                catch { /* ignore */ }

                return Ok(new { success = true, message = "CMS ready" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("/api/admin/cms/content")]
        public async Task<IActionResult> GetCmsContent()
        {
            try
            {
                var items = await _context.SiteContents.OrderBy(x => x.ContentKey).ToListAsync();
                return Ok(items.Select(x => new { x.Id, key = x.ContentKey, x.Value, x.Label, x.UpdatedAt }));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPut("/api/admin/cms/content/{id:int}")]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateCmsContent(int id, [FromBody] CmsContentUpdate dto)
        {
            var row = await _context.SiteContents.FindAsync(id);
            if (row == null) return NotFound();
            row.Value = dto.Value?.Trim() ?? row.Value;
            row.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [HttpGet("/api/admin/cms/testimonials")]
        public async Task<IActionResult> GetCmsTestimonials()
        {
            var list = await _context.HomepageTestimonials.OrderBy(x => x.SortOrder).ToListAsync();
            return Ok(list);
        }

        [HttpPost("/api/admin/cms/testimonials")]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateCmsTestimonial([FromBody] HomepageTestimonial model)
        {
            model.Id = 0;
            if (string.IsNullOrWhiteSpace(model.Quote) || string.IsNullOrWhiteSpace(model.AuthorName))
                return BadRequest(new { success = false, message = "Quote and author are required." });
            _context.HomepageTestimonials.Add(model);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, id = model.Id });
        }

        [HttpPut("/api/admin/cms/testimonials/{id:int}")]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateCmsTestimonial(int id, [FromBody] HomepageTestimonial model)
        {
            var row = await _context.HomepageTestimonials.FindAsync(id);
            if (row == null) return NotFound();
            row.Quote = model.Quote;
            row.AuthorName = model.AuthorName;
            row.AuthorMeta = model.AuthorMeta ?? "";
            row.AvatarInitials = string.IsNullOrWhiteSpace(model.AvatarInitials) ? "ST" : model.AvatarInitials;
            row.MetricLabel = model.MetricLabel;
            row.MetricValue = model.MetricValue;
            row.MetricBadge = model.MetricBadge;
            row.Tags = model.Tags;
            row.Tone = model.Tone ?? "mint";
            row.IsFeatured = model.IsFeatured;
            row.SortOrder = model.SortOrder;
            row.IsActive = model.IsActive;
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [HttpDelete("/api/admin/cms/testimonials/{id:int}")]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteCmsTestimonial(int id)
        {
            var row = await _context.HomepageTestimonials.FindAsync(id);
            if (row == null) return NotFound();
            _context.HomepageTestimonials.Remove(row);
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [HttpGet("/api/admin/cms/features")]
        public async Task<IActionResult> GetCmsFeatures()
        {
            return Ok(await _context.HomepageFeatures.OrderBy(x => x.SortOrder).ToListAsync());
        }

        [HttpPost("/api/admin/cms/features")]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateCmsFeature([FromBody] HomepageFeature model)
        {
            model.Id = 0;
            if (string.IsNullOrWhiteSpace(model.Title))
                return BadRequest(new { success = false, message = "Title is required." });
            _context.HomepageFeatures.Add(model);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, id = model.Id });
        }

        [HttpPut("/api/admin/cms/features/{id:int}")]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateCmsFeature(int id, [FromBody] HomepageFeature model)
        {
            var row = await _context.HomepageFeatures.FindAsync(id);
            if (row == null) return NotFound();
            row.Title = model.Title;
            row.Description = model.Description;
            row.Icon = model.Icon ?? row.Icon;
            row.Status = model.Status ?? row.Status;
            row.Tone = model.Tone ?? row.Tone;
            row.SortOrder = model.SortOrder;
            row.IsActive = model.IsActive;
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [HttpDelete("/api/admin/cms/features/{id:int}")]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteCmsFeature(int id)
        {
            var row = await _context.HomepageFeatures.FindAsync(id);
            if (row == null) return NotFound();
            _context.HomepageFeatures.Remove(row);
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [HttpGet("/api/admin/cms/categories")]
        public async Task<IActionResult> GetCmsCategories()
        {
            return Ok(await _context.HomepageCategoryItems.OrderBy(x => x.Group).ThenBy(x => x.SortOrder).ToListAsync());
        }

        [HttpPost("/api/admin/cms/categories")]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateCmsCategory([FromBody] HomepageCategoryItem model)
        {
            model.Id = 0;
            if (string.IsNullOrWhiteSpace(model.Name))
                return BadRequest(new { success = false, message = "Name is required." });
            if (model.Group != "Income" && model.Group != "Expense")
                model.Group = "Expense";
            _context.HomepageCategoryItems.Add(model);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, id = model.Id });
        }

        [HttpPut("/api/admin/cms/categories/{id:int}")]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateCmsCategory(int id, [FromBody] HomepageCategoryItem model)
        {
            var row = await _context.HomepageCategoryItems.FindAsync(id);
            if (row == null) return NotFound();
            row.Group = model.Group == "Income" ? "Income" : "Expense";
            row.Name = model.Name;
            row.Icon = model.Icon ?? row.Icon;
            row.Amount = model.Amount;
            row.Percent = model.Percent;
            row.SortOrder = model.SortOrder;
            row.IsActive = model.IsActive;
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [HttpDelete("/api/admin/cms/categories/{id:int}")]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteCmsCategory(int id)
        {
            var row = await _context.HomepageCategoryItems.FindAsync(id);
            if (row == null) return NotFound();
            _context.HomepageCategoryItems.Remove(row);
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [HttpGet("/api/admin/cms/proofs")]
        public async Task<IActionResult> GetCmsProofs()
        {
            return Ok(await _context.HomepageProofs.OrderBy(x => x.SortOrder).ToListAsync());
        }

        [HttpPost("/api/admin/cms/proofs")]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateCmsProof([FromBody] HomepageProof model)
        {
            model.Id = 0;
            if (string.IsNullOrWhiteSpace(model.Label) || string.IsNullOrWhiteSpace(model.Value))
                return BadRequest(new { success = false, message = "Label and value are required." });
            _context.HomepageProofs.Add(model);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, id = model.Id });
        }

        [HttpPut("/api/admin/cms/proofs/{id:int}")]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateCmsProof(int id, [FromBody] HomepageProof model)
        {
            var row = await _context.HomepageProofs.FindAsync(id);
            if (row == null) return NotFound();
            row.Label = model.Label;
            row.Value = model.Value;
            row.Badge = model.Badge ?? row.Badge;
            row.Tone = model.Tone ?? row.Tone;
            row.SortOrder = model.SortOrder;
            row.IsActive = model.IsActive;
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [HttpDelete("/api/admin/cms/proofs/{id:int}")]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteCmsProof(int id)
        {
            var row = await _context.HomepageProofs.FindAsync(id);
            if (row == null) return NotFound();
            _context.HomepageProofs.Remove(row);
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // ============== EVENT MANAGER APIs ==============
        [HttpGet("/api/admin/events")]
        public async Task<IActionResult> GetAllEvents()
        {
            try
            {
                var events = await _context.Events.OrderByDescending(e => e.EventDate).ToListAsync();
                var regCounts = await _context.EventRegistrations
                    .GroupBy(r => r.EventId)
                    .Select(g => new { EventId = g.Key, Count = g.Count() })
                    .ToListAsync();

                var result = events.Select(e => new
                {
                    id = e.Id,
                    eventCode = e.EventCode,
                    title = e.Title,
                    category = e.Category,
                    status = e.Status,
                    image = e.Image,
                    eventDate = e.EventDate.ToString("yyyy-MM-ddTHH:mm"),
                    date = e.EventDate.ToString("MMM dd, yyyy"),
                    time = e.Time,
                    venue = e.Venue,
                    fee = e.Fee,
                    seatsTotal = e.SeatsTotal,
                    seatsLeft = e.SeatsLeft,
                    registeredCount = regCounts.FirstOrDefault(r => r.EventId == e.Id)?.Count ?? 0,
                    description = e.Description,
                    autoApprove = e.AutoApprove,
                    walletIntegration = e.WalletIntegration
                });

                return Ok(result);
            }
            catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
        }

        [HttpPost("/api/admin/events")]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateEvent([FromBody] EventCreateRequest model)
        {
            try
            {
                if (model == null || string.IsNullOrWhiteSpace(model.Title))
                    return BadRequest(new { success = false, message = "Title required" });

                var eventDate = model.EventDate == default ? DateTime.UtcNow.AddDays(7) : model.EventDate;
                var seats = model.SeatsTotal > 0 ? model.SeatsTotal : 100;
                const string defaultImage = "https://images.unsplash.com/photo-1540575467063-178a50c2df87?w=500";
                string image = (model.Image ?? "").Trim();
                // Reject base64 / oversized strings — column is limited; use default banner instead
                if (string.IsNullOrWhiteSpace(image) ||
                    image.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
                    image.Length > 1000 ||
                    !(image.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                      image.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                {
                    image = defaultImage;
                }
                var evt = new Event
                {
                    EventCode = "EVT-" + new Random().Next(100, 999),
                    Title = model.Title.Trim(),
                    Category = string.IsNullOrWhiteSpace(model.Category) ? "Workshop" : model.Category.Trim(),
                    Status = "Upcoming",
                    Image = image,
                    EventDate = eventDate,
                    Time = string.IsNullOrWhiteSpace(model.Time) ? eventDate.ToString("HH:mm") : model.Time,
                    Venue = string.IsNullOrWhiteSpace(model.Venue) ? "Campus" : model.Venue.Trim(),
                    Fee = model.Fee < 0 ? 0 : model.Fee,
                    SeatsTotal = seats,
                    SeatsLeft = seats,
                    Description = model.Description,
                    AutoApprove = model.AutoApprove,
                    WalletIntegration = model.WalletIntegration,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Events.Add(evt);
                await _context.SaveChangesAsync();
                return Ok(new { success = true, id = evt.Id, eventCode = evt.EventCode });
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                return StatusCode(500, new { success = false, message = msg });
            }
        }

        [HttpPut("/api/admin/events/{id}")]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateEvent(int id, [FromBody] EventCreateRequest model)
        {
            try
            {
                var evt = await _context.Events.FindAsync(id);
                if (evt == null) return NotFound(new { success = false, message = "Event not found" });

                evt.Title = model.Title?.Trim() ?? evt.Title;
                evt.Category = model.Category ?? evt.Category;
                evt.EventDate = model.EventDate;
                evt.Venue = model.Venue ?? evt.Venue;
                evt.SeatsTotal = model.SeatsTotal;
                evt.Fee = model.Fee;
                evt.Description = model.Description ?? evt.Description;
                evt.AutoApprove = model.AutoApprove;
                evt.WalletIntegration = model.WalletIntegration;
                if (!string.IsNullOrWhiteSpace(model.Image)) evt.Image = SanitizeEventImage(model.Image);
                evt.Status = evt.SeatsLeft <= 0 ? "Housefull" : "Upcoming";

                await _context.SaveChangesAsync();
                return Ok(new { success = true });
            }
            catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
        }

        [HttpDelete("/api/admin/events/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            try
            {
                var evt = await _context.Events.FindAsync(id);
                if (evt == null) return NotFound(new { success = false, message = "Event not found" });
                _context.Events.Remove(evt);
                await _context.SaveChangesAsync();
                return Ok(new { success = true });
            }
            catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
        }

        [HttpGet("/api/admin/events/{id}/attendees")]
        public async Task<IActionResult> GetAttendees(int id)
        {
            try
            {
                var attendees = await _context.EventRegistrations
                    .Include(r => r.User)
                    .Where(r => r.EventId == id)
                    .Select(r => new
                    {
                        id = r.Id,
                        name = r.User != null ? r.User.FullName : "Unknown",
                        rollNo = "CS-2024-" + r.UserId.ToString("D3"),
                        date = r.RegisteredAt.ToString("yyyy-MM-dd"),
                        passCode = r.PassCode,
                        verified = true
                    }).ToListAsync();
                return Ok(attendees);
            }
            catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
        }

        // ============== VOUCHER MANAGEMENT APIs ==============
        [HttpGet("/api/admin/vouchers")]
        public async Task<IActionResult> GetAllVouchers()
        {
            try
            {
                var vouchers = await _context.FeeVouchers
                    .Include(v => v.User)
                    .OrderByDescending(v => v.DueDate)
                    .ToListAsync();

                var result = vouchers.Select(v => new
                {
                    id = v.Id,
                    voucherCode = v.VoucherCode,
                    studentName = v.User != null ? v.User.FullName : "Unknown",
                    rollNo = "CS-2024-" + v.UserId.ToString("D3"),
                    department = v.User != null ? (v.User.AcademicYear ?? "General") : "General",
                    title = v.Title,
                    semester = v.Semester,
                    tuition = v.Amount,
                    lab = 0,
                    discount = v.Discount,
                    fine = v.Fine,
                    total = v.Amount + v.Fine - v.Discount,
                    status = v.Status,
                    dueDate = v.DueDate.ToString("yyyy-MM-dd"),
                    issueDate = v.IssueDate.ToString("yyyy-MM-dd")
                });

                return Ok(result);
            }
            catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
        }

        [HttpPost("/api/admin/vouchers")]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateVoucher([FromBody] VoucherCreateRequest model)
        {
            try
            {
                if (model == null || string.IsNullOrWhiteSpace(model.StudentName))
                    return BadRequest(new { success = false, message = "Student name required" });

                var user = await _context.Users.FirstOrDefaultAsync(u => u.FullName == model.StudentName);
                if (user == null)
                {
                    var studentRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Student");
                    user = new User
                    {
                        FullName = model.StudentName.Trim(),
                        Email = model.StudentName.Trim().ToLower().Replace(" ", ".") + "@campus.edu",
                        RoleId = studentRole?.RoleId ?? 2,
                        AcademicYear = model.Department ?? "General",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    user.PasswordHash = _passwordHasher.HashPassword(user, GenerateTemporaryPassword());
                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();
                }

                var voucher = new FeeVoucher
                {
                    UserId = user.UserId,
                    VoucherCode = "VOU-" + new Random().Next(1000, 9999),
                    Title = model.Title ?? "Semester Fee",
                    Semester = model.Semester ?? "Semester 4",
                    Amount = model.Tuition,
                    Discount = model.Discount,
                    Fine = model.Fine,
                    DueDate = model.DueDate,
                    IssueDate = DateTime.UtcNow,
                    Status = model.Status ?? "Pending",
                    CreatedAt = DateTime.UtcNow
                };

                _context.FeeVouchers.Add(voucher);
                await _context.SaveChangesAsync();
                return Ok(new { success = true, id = voucher.Id, voucherCode = voucher.VoucherCode });
            }
            catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
        }

        [HttpPut("/api/admin/vouchers/{id}")]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateVoucher(int id, [FromBody] VoucherCreateRequest model)
        {
            try
            {
                var v = await _context.FeeVouchers.FindAsync(id);
                if (v == null) return NotFound(new { success = false, message = "Voucher not found" });
                if (model.Tuition > 0) v.Amount = model.Tuition;
                if (model.Discount >= 0) v.Discount = model.Discount;
                if (model.Fine >= 0) v.Fine = model.Fine;
                if (!string.IsNullOrWhiteSpace(model.Status)) v.Status = model.Status;
                await _context.SaveChangesAsync();
                return Ok(new { success = true });
            }
            catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
        }

        [HttpDelete("/api/admin/vouchers/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteVoucher(int id)
        {
            try
            {
                var v = await _context.FeeVouchers.FindAsync(id);
                if (v == null) return NotFound(new { success = false, message = "Voucher not found" });
                _context.FeeVouchers.Remove(v);
                await _context.SaveChangesAsync();
                return Ok(new { success = true });
            }
            catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
        }

        [HttpPost("/api/admin/vouchers/bulk-generate")]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> BulkGenerateVouchers([FromBody] BulkVoucherRequest model)
        {
            try
            {
                var students = await _context.Users
                    .Where(u => u.AcademicYear == model.Department)
                    .Take(4).ToListAsync();

                if (!students.Any())
                {
                    students = await _context.Users
                        .Where(u => u.Role != null && u.Role.RoleName == "Student")
                        .Take(4).ToListAsync();
                }

                if (!students.Any())
                    return BadRequest(new { success = false, message = "No students found" });

                var newVouchers = new List<FeeVoucher>();
                foreach (var student in students)
                {
                    newVouchers.Add(new FeeVoucher
                    {
                        UserId = student.UserId,
                        VoucherCode = "VOU-" + new Random().Next(1000, 9999),
                        Title = "Semester Fee - " + model.Batch,
                        Semester = model.Batch ?? "Fall 2026",
                        Amount = model.Tuition,
                        Discount = 0,
                        Fine = 0,
                        DueDate = model.DueDate,
                        IssueDate = DateTime.UtcNow,
                        Status = "Pending",
                        CreatedAt = DateTime.UtcNow
                    });
                }

                _context.FeeVouchers.AddRange(newVouchers);
                await _context.SaveChangesAsync();
                return Ok(new { success = true, count = newVouchers.Count });
            }
            catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
        }

        [HttpPost("/api/admin/vouchers/config")]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public IActionResult SaveFeeConfig([FromBody] FeeConfigRequest model)
        {
            return Ok(new { success = true, message = $"Config saved" });
        }


        // ============== SPENDING ALERTS (admin — all students) ==============
        [HttpGet("/api/admin/spending-alerts")]
        public async Task<IActionResult> GetAdminSpendingAlerts()
        {
            try
            {
                var now = DateTime.UtcNow;
                var monthStart = new DateTime(now.Year, now.Month, 1);
                var monthEnd = monthStart.AddMonths(1);

                var budgets = await _context.Budgets
                    .Include(b => b.Category)
                    .Include(b => b.User)
                    .Where(b => b.Month == monthStart && b.LimitAmount > 0)
                    .ToListAsync();

                var rows = new List<object>();
                foreach (var b in budgets)
                {
                    var spent = await _context.Transactions
                        .Where(t => t.UserId == b.UserId && t.CategoryId == b.CategoryId
                            && t.Type == "Expense" && t.Date >= monthStart && t.Date < monthEnd)
                        .SumAsync(t => t.Amount);
                    if (b.LimitAmount <= 0) continue;
                    var pct = Math.Round(spent / b.LimitAmount * 100m, 0);
                    if (pct < 80) continue;
                    var remaining = b.LimitAmount - spent;
                    var severity = pct >= 100 ? "over" : "warning";
                    rows.Add(new
                    {
                        budgetId = b.BudgetId,
                        userId = b.UserId,
                        studentName = b.User != null ? b.User.FullName : "Unknown",
                        studentEmail = b.User != null ? b.User.Email : "",
                        categoryId = b.CategoryId,
                        category = b.Category != null ? b.Category.Name : "Category",
                        limit = b.LimitAmount,
                        spent,
                        remaining,
                        percent = pct,
                        severity,
                        message = pct >= 100
                            ? $"{(b.User != null ? b.User.FullName : "Student")} used {pct}% of {b.Category?.Name} — over by ${Math.Abs(remaining):F2}"
                            : $"{(b.User != null ? b.User.FullName : "Student")} at {pct}% of {b.Category?.Name} — Rs. {remaining:F2} left"
                    });
                }

                var ordered = rows; // client can sort; already filtered
                return Ok(new { success = true, alerts = ordered, month = monthStart.ToString("yyyy-MM") });
            }
            catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
        }

        [HttpPost("/api/admin/spending-alerts/notify")]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> NotifySpendingAlert([FromBody] AdminAlertNotifyRequest model)
        {
            try
            {
                if (model == null || model.UserId <= 0)
                    return BadRequest(new { success = false, message = "User required" });
                var msg = string.IsNullOrWhiteSpace(model.Message)
                    ? "Admin notice: Please review your budget — you are near or over your category limit."
                    : model.Message.Trim();
                _context.Notifications.Add(new Notification
                {
                    UserId = model.UserId,
                    Message = msg,
                    Type = "Alert",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Student notified." });
            }
            catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
        }

        // Adjust budget limit from alerts panel (uses existing Budget row)
        [HttpPut("/api/admin/spending-alerts/budget/{budgetId}")]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateAlertBudget(int budgetId, [FromBody] AdminAlertBudgetUpdate model)
        {
            try
            {
                var b = await _context.Budgets.FindAsync(budgetId);
                if (b == null) return NotFound(new { success = false, message = "Budget not found" });
                if (model.LimitAmount <= 0)
                    return BadRequest(new { success = false, message = "Limit must be positive" });
                b.LimitAmount = model.LimitAmount;
                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Budget limit updated." });
            }
            catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
        }

        [HttpDelete("/api/admin/spending-alerts/budget/{budgetId}")]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteAlertBudget(int budgetId)
        {
            try
            {
                var b = await _context.Budgets.FindAsync(budgetId);
                if (b == null) return NotFound(new { success = false, message = "Budget not found" });
                _context.Budgets.Remove(b);
                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Budget removed." });
            }
            catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
        }

        // ============== EXPENSE COMPARISON (admin) ==============
        [HttpGet("/api/admin/expense-comparison")]
        public async Task<IActionResult> GetAdminExpenseComparison([FromQuery] int? userId = null)
        {
            try
            {
                var now = DateTime.UtcNow;
                var thisStart = new DateTime(now.Year, now.Month, 1);
                var thisEnd = thisStart.AddMonths(1);
                var lastStart = thisStart.AddMonths(-1);

                var q = _context.Transactions.Include(t => t.Category).Where(t => t.Type == "Expense");
                if (userId.HasValue && userId.Value > 0)
                    q = q.Where(t => t.UserId == userId.Value);

                var thisMonth = await q.Where(t => t.Date >= thisStart && t.Date < thisEnd)
                    .GroupBy(t => t.Category != null ? t.Category.Name : "Other")
                    .Select(g => new { Name = g.Key, Total = g.Sum(x => x.Amount) })
                    .ToListAsync();

                var lastMonth = await q.Where(t => t.Date >= lastStart && t.Date < thisStart)
                    .GroupBy(t => t.Category != null ? t.Category.Name : "Other")
                    .Select(g => new { Name = g.Key, Total = g.Sum(x => x.Amount) })
                    .ToListAsync();

                var names = thisMonth.Select(x => x.Name).Union(lastMonth.Select(x => x.Name)).OrderBy(n => n).ToList();
                var labels = new List<string>();
                var thisData = new List<decimal>();
                var lastData = new List<decimal>();
                var categories = new List<object>();

                foreach (var name in names)
                {
                    var t = thisMonth.FirstOrDefault(x => x.Name == name)?.Total ?? 0;
                    var l = lastMonth.FirstOrDefault(x => x.Name == name)?.Total ?? 0;
                    decimal changePct = 0;
                    if (l > 0) changePct = Math.Round((t - l) / l * 100m, 1);
                    else if (t > 0) changePct = 100;
                    labels.Add(name);
                    thisData.Add(t);
                    lastData.Add(l);
                    categories.Add(new { category = name, thisMonth = t, lastMonth = l, changePercent = changePct, direction = changePct > 0 ? "up" : (changePct < 0 ? "down" : "same") });
                }

                var students = await _context.Users.Include(u => u.Role)
                    .Where(u => u.Role != null && u.Role.RoleName == "Student")
                    .OrderBy(u => u.FullName)
                    .Select(u => new { userId = u.UserId, fullName = u.FullName, email = u.Email })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    labels,
                    thisMonth = thisData,
                    lastMonth = lastData,
                    categories,
                    thisMonthLabel = thisStart.ToString("MMM yyyy"),
                    lastMonthLabel = lastStart.ToString("MMM yyyy"),
                    students
                });
            }
            catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
        }


        // ============== SETTINGS ==============
        [HttpGet("/api/admin/settings/profile")]
        public async Task<IActionResult> GetAdminProfile()
        {
            int adminId = GetCurrentUserId();
            var user = await _context.Users.FindAsync(adminId);
            if (user == null) return NotFound();
            return Ok(new
            {
                userId = user.UserId,
                fullName = user.FullName,
                email = user.Email,
                role = "Administrator",
                createdAt = user.CreatedAt.ToString("yyyy-MM-dd")
            });
        }

        [HttpPut("/api/admin/settings/profile")]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateAdminProfile([FromBody] AdminProfileRequest model)
        {
            try
            {
                int adminId = GetCurrentUserId();
                var user = await _context.Users.FindAsync(adminId);
                if (user == null) return NotFound();

                if (!string.IsNullOrWhiteSpace(model.FullName)) user.FullName = model.FullName.Trim();
                if (!string.IsNullOrWhiteSpace(model.Email))
                {
                    string e = model.Email.Trim().ToLower();
                    if (await _context.Users.AnyAsync(u => u.Email.ToLower() == e && u.UserId != adminId))
                        return BadRequest(new { success = false, message = "Email already in use." });
                    user.Email = model.Email.Trim();
                }
                await _context.SaveChangesAsync();
                return Ok(new { success = true });
            }
            catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
        }

        [HttpPut("/api/admin/settings/password")]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ChangeAdminPassword([FromBody] AdminPasswordRequest model)
        {
            try
            {
                int adminId = GetCurrentUserId();
                var user = await _context.Users.FindAsync(adminId);
                if (user == null) return NotFound();

                if (string.IsNullOrWhiteSpace(model.NewPassword) || model.NewPassword.Length < 6)
                    return BadRequest(new { success = false, message = "Password must be at least 6 characters." });
                if (model.NewPassword != model.ConfirmPassword)
                    return BadRequest(new { success = false, message = "Passwords do not match." });

                user.PasswordHash = _passwordHasher.HashPassword(user, model.NewPassword.Trim());
                await _context.SaveChangesAsync();
                return Ok(new { success = true });
            }
            catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
        }

        // ============== STUDENTS CRUD ==============
        [HttpGet("/api/admin/students")]
        public async Task<IActionResult> GetStudentsList()
        {
            try
            {
                var users = await _context.Users.Include(u => u.Role).Include(u => u.Transactions)
                    .Where(u => u.Role != null && u.Role.RoleName == "Student")
                    .OrderByDescending(u => u.CreatedAt).ToListAsync();

                var healthScores = await _context.FinancialHealthScores.ToListAsync();

                var result = users.Select(u =>
                {
                    decimal income = u.Transactions.Where(t => t.Type == "Income").Sum(t => t.Amount);
                    decimal expense = u.Transactions.Where(t => t.Type == "Expense").Sum(t => t.Amount);
                    var hs = healthScores.FirstOrDefault(h => h.UserId == u.UserId);
                    int health = hs != null ? Convert.ToInt32(hs.OverallScore) : 75;
                    string status = !u.IsActive ? "Inactive" : (health < 60 ? "At Risk" : "Active");

                    return new
                    {
                        id = u.UserId,
                        name = u.FullName,
                        studentId = $"STU-{1000 + u.UserId}",
                        email = u.Email,
                        department = string.IsNullOrWhiteSpace(u.AcademicYear) ? "General" : u.AcademicYear,
                        status,
                        health,
                        joined = u.CreatedAt.ToString("yyyy-MM-dd"),
                        transactions = u.Transactions.Count,
                        balance = income - expense
                    };
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
        }

        [HttpGet("/api/admin/students/{id}")]
        public async Task<IActionResult> GetStudentById(int id)
        {
            try
            {
                var user = await _context.Users.Include(u => u.Role).Include(u => u.Transactions).ThenInclude(t => t.Category)
                    .FirstOrDefaultAsync(u => u.UserId == id);
                if (user == null) return NotFound(new { message = "Student not found." });

                var hs = await _context.FinancialHealthScores.FirstOrDefaultAsync(h => h.UserId == id);
                decimal income = user.Transactions.Where(t => t.Type == "Income").Sum(t => t.Amount);
                decimal expense = user.Transactions.Where(t => t.Type == "Expense").Sum(t => t.Amount);
                int health = hs != null ? Convert.ToInt32(hs.OverallScore) : 75;
                string status = !user.IsActive ? "Inactive" : (health < 60 ? "At Risk" : "Active");

                var recentTxns = user.Transactions.OrderByDescending(t => t.CreatedAt).Take(5)
                    .Select(t => new { id = t.TransactionId, category = t.Category != null ? t.Category.Name : "Other", amount = t.Amount, type = t.Type, date = t.Date.ToString("yyyy-MM-dd") }).ToList();

                return Ok(new
                {
                    id = user.UserId,
                    name = user.FullName,
                    studentId = $"STU-{1000 + user.UserId}",
                    email = user.Email,
                    department = string.IsNullOrWhiteSpace(user.AcademicYear) ? "General" : user.AcademicYear,
                    status,
                    health,
                    joined = user.CreatedAt.ToString("yyyy-MM-dd"),
                    transactions = user.Transactions.Count,
                    balance = income - expense,
                    totalIncome = income,
                    totalExpense = expense,
                    recentTransactions = recentTxns
                });
            }
            catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
        }

        [HttpPost("/api/admin/students")]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateStudent([FromBody] StudentUpsertRequest model)
        {
            try
            {
                if (model == null) return BadRequest(new { success = false, message = "No data received." });
                if (string.IsNullOrWhiteSpace(model.FullName) || string.IsNullOrWhiteSpace(model.Email))
                    return BadRequest(new { success = false, message = "Name and email are required." });

                string emailLower = model.Email.Trim().ToLower();
                if (await _context.Users.AnyAsync(u => u.Email.ToLower() == emailLower))
                    return BadRequest(new { success = false, message = "Email already registered." });

                var studentRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Student");
                if (studentRole == null) return BadRequest(new { success = false, message = "Student role not found." });

                var user = new User
                {
                    FullName = model.FullName.Trim(),
                    Email = model.Email.Trim(),
                    RoleId = studentRole.RoleId,
                    AcademicYear = string.IsNullOrWhiteSpace(model.Department) ? "General" : model.Department,
                    IsActive = !string.Equals(model.Status, "Inactive", StringComparison.OrdinalIgnoreCase),
                    CreatedAt = DateTime.UtcNow
                };
                bool generatedPassword = string.IsNullOrWhiteSpace(model.Password);
                string pass = generatedPassword ? GenerateTemporaryPassword() : model.Password.Trim();
                user.PasswordHash = _passwordHasher.HashPassword(user, pass);
                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                try
                {
                    _context.FinancialHealthScores.Add(new FinancialHealthScore { UserId = user.UserId, OverallScore = model.Health });
                    await _context.SaveChangesAsync();
                }
                catch { }

                return Ok(new { success = true, id = user.UserId, temporaryPassword = generatedPassword ? pass : null });
            }
            catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
        }

        [HttpPut("/api/admin/students/{id}")]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateStudent(int id, [FromBody] StudentUpsertRequest model)
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == id);
                if (user == null) return NotFound(new { message = "Student not found." });

                user.FullName = string.IsNullOrWhiteSpace(model.FullName) ? user.FullName : model.FullName.Trim();
                user.Email = string.IsNullOrWhiteSpace(model.Email) ? user.Email : model.Email.Trim();
                user.AcademicYear = string.IsNullOrWhiteSpace(model.Department) ? user.AcademicYear : model.Department;
                user.IsActive = !string.Equals(model.Status, "Inactive", StringComparison.OrdinalIgnoreCase);

                if (!string.IsNullOrWhiteSpace(model.Password))
                    user.PasswordHash = _passwordHasher.HashPassword(user, model.Password.Trim());

                try
                {
                    var hs = await _context.FinancialHealthScores.FirstOrDefaultAsync(h => h.UserId == id);
                    if (hs != null) hs.OverallScore = model.Health;
                    else _context.FinancialHealthScores.Add(new FinancialHealthScore { UserId = id, OverallScore = model.Health });
                }
                catch { }

                await _context.SaveChangesAsync();
                return Ok(new { success = true });
            }
            catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
        }

        [HttpDelete("/api/admin/students/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteStudent(int id)
        {
            try
            {
                var user = await _context.Users.Include(u => u.Transactions).FirstOrDefaultAsync(u => u.UserId == id);
                if (user == null) return NotFound(new { message = "Student not found." });
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
                return Ok(new { success = true });
            }
            catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
        }

        [HttpPost("/api/admin/students/bulk-activate")]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> BulkActivateStudents([FromBody] BulkStudentRequest model)
        {
            try
            {
                if (model?.Ids == null || !model.Ids.Any())
                    return BadRequest(new { success = false, message = "No students selected." });
                var users = await _context.Users.Where(u => model.Ids.Contains(u.UserId)).ToListAsync();
                foreach (var u in users) u.IsActive = true;
                await _context.SaveChangesAsync();
                return Ok(new { success = true, count = users.Count });
            }
            catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
        }

        [HttpPost("/api/admin/students/bulk-delete")]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> BulkDeleteStudents([FromBody] BulkStudentRequest model)
        {
            try
            {
                if (model?.Ids == null || !model.Ids.Any())
                    return BadRequest(new { success = false, message = "No students selected." });
                var users = await _context.Users.Include(u => u.Transactions)
                    .Where(u => model.Ids.Contains(u.UserId)).ToListAsync();
                _context.Users.RemoveRange(users);
                await _context.SaveChangesAsync();
                return Ok(new { success = true, count = users.Count });
            }
            catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
        }

        // ============== TRANSACTIONS ==============
        [HttpGet("/api/admin/transactions")]
        public async Task<IActionResult> GetTransactionsList()
        {
            try
            {
                var txns = await _context.Transactions.Include(t => t.User).Include(t => t.Category)
                    .OrderByDescending(t => t.CreatedAt).ToListAsync();

                var result = txns.Select(t =>
                {
                    var name = t.User?.FullName ?? "Unknown";
                    var initials = string.IsNullOrEmpty(name) ? "??" : string.Join("", name.Split(' ').Take(2).Select(n => n.Length > 0 ? n[0].ToString() : "")).ToUpper();
                    return new
                    {
                        id = t.TransactionId,
                        studentName = name,
                        studentEmail = t.User?.Email ?? "",
                        initials,
                        studentId = $"STU-{1000 + (t.User?.UserId ?? 0)}",
                        categoryId = t.CategoryId,
                        category = t.Category?.Name ?? "Other",
                        type = t.Type,
                        amount = t.Amount,
                        date = t.Date.ToString("yyyy-MM-dd"),
                        note = "",
                        createdAt = t.CreatedAt.ToString("yyyy-MM-dd HH:mm")
                    };
                }).ToList();

                decimal totalIncome = txns.Where(t => t.Type == "Income").Sum(t => t.Amount);
                decimal totalExpense = txns.Where(t => t.Type == "Expense").Sum(t => t.Amount);

                return Ok(new
                {
                    transactions = result,
                    summary = new { totalCount = txns.Count, totalIncome, totalExpense, net = totalIncome - totalExpense }
                });
            }
            catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
        }

        [HttpDelete("/api/admin/transactions/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteTransaction(int id)
        {
            try
            {
                var exists = await _context.Transactions.AnyAsync(t => t.TransactionId == id);
                if (!exists) return NotFound(new { success = false, message = "Transaction not found." });
                await _context.Transactions.Where(t => t.TransactionId == id).ExecuteDeleteAsync();
                return Ok(new { success = true });
            }
            catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
        }

        [HttpPut("/api/admin/transactions/{id}")]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateTransaction(int id, [FromBody] AdminTransactionUpsertRequest model)
        {
            try
            {
                var t = await _context.Transactions.FirstOrDefaultAsync(x => x.TransactionId == id);
                if (t == null) return NotFound(new { success = false, message = "Transaction not found." });
                if (model.Amount > 0) t.Amount = model.Amount;
                if (!string.IsNullOrWhiteSpace(model.Type)) t.Type = model.Type;
                if (model.CategoryId.HasValue && model.CategoryId.Value > 0) t.CategoryId = model.CategoryId.Value;
                if (model.Date.HasValue) t.Date = model.Date.Value;
                await _context.SaveChangesAsync();
                return Ok(new { success = true });
            }
            catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
        }

        // ============== CATEGORIES ==============
        [HttpGet("/api/admin/categories")]
        public async Task<IActionResult> GetDefaultCategories()
        {
            var categories = await _context.Categories.Where(c => c.IsDefault)
                .OrderBy(c => c.Type).ThenBy(c => c.Name).ToListAsync();
            return Ok(categories);
        }

        [HttpPost("/api/admin/categories")]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddDefaultCategory([FromBody] AddDefaultCategoryRequest model)
        {
            if (string.IsNullOrWhiteSpace(model.Name))
                return BadRequest(new { success = false, message = "Name is required." });
            var category = new Category
            {
                Name = model.Name.Trim(),
                Type = model.Type.Equals("Income", StringComparison.OrdinalIgnoreCase) ? "Income" : "Expense",
                IsDefault = true,
                UserId = null
            };
            _context.Categories.Add(category);
            await _context.SaveChangesAsync();
            return Ok(category);
        }

        [HttpPut("/api/admin/categories/{id}")]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateDefaultCategory(int id, [FromBody] AddDefaultCategoryRequest model)
        {
            try
            {
                var category = await _context.Categories.FirstOrDefaultAsync(c => c.CategoryId == id && c.IsDefault);
                if (category == null) return NotFound(new { message = "Category not found." });
                if (string.IsNullOrWhiteSpace(model.Name))
                    return BadRequest(new { success = false, message = "Name is required." });
                category.Name = model.Name.Trim();
                category.Type = model.Type.Equals("Income", StringComparison.OrdinalIgnoreCase) ? "Income" : "Expense";
                await _context.SaveChangesAsync();
                return Ok(new { success = true });
            }
            catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
        }

        [HttpDelete("/api/admin/categories/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteDefaultCategory(int id)
        {
            var category = await _context.Categories.FirstOrDefaultAsync(c => c.CategoryId == id && c.IsDefault);
            if (category == null) return NotFound();
            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // ============== BUDGETS ==============
        [HttpGet("/api/admin/budgets")]
        public async Task<IActionResult> GetBudgetsList()
        {
            try
            {
                var budgets = await _context.Budgets.Include(b => b.User).Include(b => b.Category)
                    .OrderByDescending(b => b.BudgetId).ToListAsync();

                var result = new List<object>();
                foreach (var b in budgets)
                {
                    var used = await _context.Transactions
                        .Where(t => t.Type == "Expense" && t.UserId == b.UserId && t.CategoryId == b.CategoryId)
                        .SumAsync(t => (decimal?)t.Amount) ?? 0;

                    result.Add(new
                    {
                        id = b.BudgetId,
                        userId = b.UserId,
                        student = b.User != null ? $"{b.User.FullName} ({b.User.AcademicYear ?? "N/A"})" : "Unknown",
                        studentName = b.User != null ? b.User.FullName : "Unknown",
                        categoryId = b.CategoryId,
                        category = b.Category != null ? b.Category.Name : "Other",
                        limit = b.LimitAmount,
                        used = used,
                        threshold = 85
                    });
                }
                return Ok(result);
            }
            catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
        }

        [HttpGet("/api/admin/budgets/students")]
        public async Task<IActionResult> GetBudgetStudents()
        {
            var students = await _context.Users
                .Where(u => u.Role != null && u.Role.RoleName == "Student")
                .Select(u => new { u.UserId, u.FullName, u.AcademicYear }).ToListAsync();
            return Ok(students);
        }

        [HttpGet("/api/admin/budgets/categories")]
        public async Task<IActionResult> GetBudgetCategories()
        {
            var cats = await _context.Categories
                .Where(c => c.IsDefault && c.Type == "Expense")
                .Select(c => new { c.CategoryId, c.Name }).ToListAsync();
            return Ok(cats);
        }

        [HttpPost("/api/admin/budgets")]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateBudget([FromBody] BudgetUpsertRequest model)
        {
            try
            {
                var budget = new Budget
                {
                    UserId = model.UserId,
                    CategoryId = model.CategoryId,
                    LimitAmount = model.Limit,
                    Month = DateTime.UtcNow
                };
                _context.Budgets.Add(budget);
                await _context.SaveChangesAsync();
                return Ok(new { success = true, id = budget.BudgetId });
            }
            catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
        }

        [HttpPut("/api/admin/budgets/{id}")]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateBudget(int id, [FromBody] BudgetUpsertRequest model)
        {
            try
            {
                var b = await _context.Budgets.FindAsync(id);
                if (b == null) return NotFound(new { message = "Budget not found." });
                b.UserId = model.UserId;
                b.CategoryId = model.CategoryId;
                b.LimitAmount = model.Limit;
                await _context.SaveChangesAsync();
                return Ok(new { success = true });
            }
            catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
        }

        [HttpDelete("/api/admin/budgets/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteBudget(int id)
        {
            try
            {
                var b = await _context.Budgets.FindAsync(id);
                if (b == null) return NotFound();
                _context.Budgets.Remove(b);
                await _context.SaveChangesAsync();
                return Ok(new { success = true });
            }
            catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
        }

        [HttpPost("/api/admin/budgets/{id}/notify")]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> NotifyBudgetStudent(int id)
        {
            try
            {
                var budget = await _context.Budgets.Include(b => b.User).Include(b => b.Category)
                    .FirstOrDefaultAsync(b => b.BudgetId == id);
                if (budget == null || budget.User == null)
                    return NotFound(new { success = false, message = "Budget or student not found." });

                var used = await _context.Transactions
                    .Where(t => t.Type == "Expense" && t.UserId == budget.UserId && t.CategoryId == budget.CategoryId)
                    .SumAsync(t => (decimal?)t.Amount) ?? 0;

                int percent = budget.LimitAmount > 0 ? (int)Math.Round((used / budget.LimitAmount) * 100) : 0;
                string msg = percent >= 100
                    ? $"🚨 Budget Alert: Exceeded {budget.Category?.Name} budget by Rs. {(used - budget.LimitAmount):F2} ({percent}% used)."
                    : percent >= 85
                        ? $"⚠️ Budget Alert: {percent}% of {budget.Category?.Name} budget used (Rs. {used:F2} of Rs. {budget.LimitAmount:F2})."
                        : $"ℹ️ Reminder: {percent}% of {budget.Category?.Name} budget used.";

                _context.Notifications.Add(new Notification
                {
                    UserId = budget.UserId,
                    Message = msg,
                    Type = percent >= 100 ? "Warning" : "Info",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
                return Ok(new { success = true, percent, sentTo = budget.User.Email });
            }
            catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
        }

        // ============== SAVING GOALS ==============
        [HttpGet("/api/admin/saving-goals")]
        public async Task<IActionResult> GetSavingGoalsList()
        {
            try
            {
                var goals = await _context.SavingsGoals.Include(g => g.User)
                    .OrderByDescending(g => g.GoalId).ToListAsync();

                var palette = new[] { "#38bdf8", "#10b981", "#6366f1", "#f59e0b", "#ec4899", "#22d3ee" };
                var icons = new[] { "fa-bullseye", "fa-laptop", "fa-plane", "fa-shield-halved", "fa-certificate", "fa-piggy-bank" };

                var result = goals.Select((g, i) => new
                {
                    id = g.GoalId,
                    userId = g.UserId,
                    student = g.User != null ? g.User.FullName : "Unknown",
                    title = g.GoalName,
                    target = g.TargetAmount,
                    saved = g.CurrentAmount,
                    isAchieved = g.IsAchieved,
                    date = g.TargetDate?.ToString("yyyy-MM-dd") ?? "",
                    icon = icons[i % icons.Length],
                    color = palette[i % palette.Length]
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
        }

        [HttpPost("/api/admin/saving-goals")]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateSavingGoal([FromBody] SavingGoalUpsertRequest model)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(model.Title))
                    return BadRequest(new { success = false, message = "Goal title is required." });

                var goal = new SavingsGoal
                {
                    UserId = model.UserId,
                    GoalName = model.Title.Trim(),
                    TargetAmount = model.Target,
                    CurrentAmount = model.Saved,
                    IsAchieved = model.Saved >= model.Target,
                    TargetDate = model.Date != default ? model.Date : DateTime.UtcNow.AddMonths(6)
                };
                _context.SavingsGoals.Add(goal);
                await _context.SaveChangesAsync();
                return Ok(new { success = true, id = goal.GoalId });
            }
            catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
        }

        [HttpPut("/api/admin/saving-goals/{id}")]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateSavingGoal(int id, [FromBody] SavingGoalUpsertRequest model)
        {
            try
            {
                var g = await _context.SavingsGoals.FindAsync(id);
                if (g == null) return NotFound(new { message = "Goal not found." });
                g.GoalName = model.Title.Trim();
                g.TargetAmount = model.Target;
                g.CurrentAmount = model.Saved;
                g.IsAchieved = model.Saved >= model.Target;
                if (model.Date != default) g.TargetDate = model.Date;
                await _context.SaveChangesAsync();
                return Ok(new { success = true });
            }
            catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
        }

        [HttpDelete("/api/admin/saving-goals/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteSavingGoal(int id)
        {
            try
            {
                var g = await _context.SavingsGoals.FindAsync(id);
                if (g == null) return NotFound();
                _context.SavingsGoals.Remove(g);
                await _context.SaveChangesAsync();
                return Ok(new { success = true });
            }
            catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
        }

        [HttpPost("/api/admin/saving-goals/{id}/deposit")]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DepositToGoal(int id, [FromBody] DepositRequest model)
        {
            try
            {
                var g = await _context.SavingsGoals.FindAsync(id);
                if (g == null) return NotFound();
                g.CurrentAmount = Math.Min(g.TargetAmount, g.CurrentAmount + model.Amount);
                g.IsAchieved = g.CurrentAmount >= g.TargetAmount;
                await _context.SaveChangesAsync();
                return Ok(new { success = true, current = g.CurrentAmount, isAchieved = g.IsAchieved });
            }
            catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
        }

        // ============== REPORTS ==============
        [HttpGet("/api/admin/reports")]
        public async Task<IActionResult> GetReports(
            [FromQuery] string timeframe = "month",
            [FromQuery] string? dateFrom = null,
            [FromQuery] string? dateTo = null,
            [FromQuery] int? categoryId = null)
        {
            try
            {
                var now = DateTime.UtcNow;
                DateTime fromDate;
                DateTime toDate = now.AddDays(1);

                if (!string.IsNullOrWhiteSpace(dateFrom) && DateTime.TryParse(dateFrom, out var parsedFrom))
                {
                    fromDate = parsedFrom.Date;
                    if (!string.IsNullOrWhiteSpace(dateTo) && DateTime.TryParse(dateTo, out var parsedTo))
                        toDate = parsedTo.Date.AddDays(1);
                }
                else
                {
                    switch ((timeframe ?? "month").ToLower())
                    {
                        case "quarter": fromDate = now.AddMonths(-3); break;
                        case "year": fromDate = new DateTime(now.Year, 1, 1); break;
                        default: fromDate = new DateTime(now.Year, now.Month, 1); break;
                    }
                }

                var txnsQuery = _context.Transactions.Where(t => t.Date >= fromDate && t.Date < toDate);
                if (categoryId.HasValue && categoryId.Value > 0)
                    txnsQuery = txnsQuery.Where(t => t.CategoryId == categoryId.Value);

                var allTxns = await txnsQuery.ToListAsync();
                decimal totalIncome = allTxns.Where(t => t.Type == "Income").Sum(t => t.Amount);
                decimal totalExpense = allTxns.Where(t => t.Type == "Expense").Sum(t => t.Amount);

                var healthScores = await _context.FinancialHealthScores.ToListAsync();
                double avgHealth = healthScores.Any() ? healthScores.Average(h => (double)h.OverallScore) : 78;

                var monthLabels = new List<string>();
                var incomeData = new List<decimal>();
                var expenseData = new List<decimal>();
                var baseMonth = new DateTime(now.Year, now.Month, 1);

                for (int i = 5; i >= 0; i--)
                {
                    var mStart = baseMonth.AddMonths(-i);
                    var mEnd = mStart.AddMonths(1);
                    monthLabels.Add(mStart.ToString("MMM"));
                    var monthQ = _context.Transactions.Where(t => t.Date >= mStart && t.Date < mEnd);
                    if (categoryId.HasValue && categoryId.Value > 0)
                        monthQ = monthQ.Where(t => t.CategoryId == categoryId.Value);
                    var monthTxns = await monthQ.ToListAsync();
                    incomeData.Add(monthTxns.Where(t => t.Type == "Income").Sum(t => t.Amount));
                    expenseData.Add(monthTxns.Where(t => t.Type == "Expense").Sum(t => t.Amount));
                }

                var expenseQuery = _context.Transactions.Include(t => t.Category)
                    .Where(t => t.Type == "Expense" && t.Date >= fromDate && t.Date < toDate);
                if (categoryId.HasValue && categoryId.Value > 0)
                    expenseQuery = expenseQuery.Where(t => t.CategoryId == categoryId.Value);
                var expenseTxns = await expenseQuery.ToListAsync();

                var categoryGroups = expenseTxns
                    .GroupBy(t => t.Category != null ? t.Category.Name : "Other")
                    .Select(g => new { name = g.Key, total = g.Sum(x => x.Amount) })
                    .OrderByDescending(x => x.total).ToList();

                var palette = new[] { "#38bdf8", "#0ea5e9", "#6366f1", "#818cf8", "#a78bfa", "#22d3ee", "#f59e0b", "#10b981", "#f43f5e" };
                var catLabels = categoryGroups.Select(c => c.name).ToList();
                var catValues = categoryGroups.Select(c => (double)c.total).ToList();
                var catColors = catLabels.Select((_, i) => palette[i % palette.Length]).ToList();

                var activityLabels = new List<string>();
                var activityCounts = new List<int>();
                for (int i = 6; i >= 0; i--)
                {
                    var day = now.Date.AddDays(-i);
                    var nextDay = day.AddDays(1);
                    activityLabels.Add(day.ToString("ddd"));
                    var dayQ = _context.Transactions.Where(t => t.Date >= day && t.Date < nextDay);
                    if (categoryId.HasValue && categoryId.Value > 0)
                        dayQ = dayQ.Where(t => t.CategoryId == categoryId.Value);
                    activityCounts.Add(await dayQ.CountAsync());
                }

                var today = now.Date;
                int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
                var weekStart = today.AddDays(-diff);
                var weekEnd = weekStart.AddDays(7);
                var weekQ = _context.Transactions.Where(t => t.Date >= weekStart && t.Date < weekEnd);
                if (categoryId.HasValue && categoryId.Value > 0)
                    weekQ = weekQ.Where(t => t.CategoryId == categoryId.Value);
                var weekTxns = await weekQ.ToListAsync();

                var budgets = await _context.Budgets.ToListAsync();
                if (categoryId.HasValue && categoryId.Value > 0)
                    budgets = budgets.Where(b => b.CategoryId == categoryId.Value).ToList();

                var budgetLabels = new List<string>();
                var budgetPlanned = new List<decimal>();
                var budgetActual = new List<decimal>();
                foreach (var b in budgets.Take(8))
                {
                    var cat = await _context.Categories.FirstOrDefaultAsync(c => c.CategoryId == b.CategoryId);
                    budgetLabels.Add(cat != null ? cat.Name : $"Cat #{b.CategoryId}");
                    budgetPlanned.Add(b.LimitAmount);
                    var spent = await _context.Transactions.Where(t => t.Type == "Expense" && t.CategoryId == b.CategoryId).SumAsync(t => (decimal?)t.Amount) ?? 0;
                    budgetActual.Add(spent);
                }

                var savingLabels = new List<string>();
                var savingTotals = new List<decimal>();
                decimal cumulative = 0;
                for (int i = 5; i >= 0; i--)
                {
                    var mStart = baseMonth.AddMonths(-i);
                    var mEnd = mStart.AddMonths(1);
                    savingLabels.Add(mStart.ToString("MMM"));
                    var mIncQ = _context.Transactions.Where(t => t.Type == "Income" && t.Date >= mStart && t.Date < mEnd);
                    var mExpQ = _context.Transactions.Where(t => t.Type == "Expense" && t.Date >= mStart && t.Date < mEnd);
                    if (categoryId.HasValue && categoryId.Value > 0)
                    {
                        mIncQ = mIncQ.Where(t => t.CategoryId == categoryId.Value);
                        mExpQ = mExpQ.Where(t => t.CategoryId == categoryId.Value);
                    }
                    var mIncome = await mIncQ.SumAsync(t => (decimal?)t.Amount) ?? 0;
                    var mExpense = await mExpQ.SumAsync(t => (decimal?)t.Amount) ?? 0;
                    cumulative += (mIncome - mExpense);
                    savingTotals.Add(cumulative);
                }

                var categoryTable = new List<object>();
                foreach (var b in budgets)
                {
                    var cat = await _context.Categories.FirstOrDefaultAsync(c => c.CategoryId == b.CategoryId);
                    var spent = await _context.Transactions.Where(t => t.Type == "Expense" && t.CategoryId == b.CategoryId).SumAsync(t => (decimal?)t.Amount) ?? 0;
                    decimal variance = b.LimitAmount - spent;
                    categoryTable.Add(new
                    {
                        category = cat != null ? cat.Name : $"Category #{b.CategoryId}",
                        budgeted = b.LimitAmount,
                        actual = spent,
                        variance = variance,
                        status = variance >= 0 ? "Healthy" : "Over Budget"
                    });
                }

                var mostUsed = await _context.Transactions.Include(t => t.Category)
                    .Where(t => t.Date >= fromDate && t.Date < toDate)
                    .GroupBy(t => t.Category != null ? t.Category.Name : "Other")
                    .Select(g => new { name = g.Key, count = g.Count(), volume = g.Sum(x => x.Amount) })
                    .OrderByDescending(x => x.count).Take(5).ToListAsync();

                var allCats = await _context.Categories.Where(c => c.IsDefault)
                    .Select(c => new { c.CategoryId, c.Name, c.Type }).ToListAsync();

                return Ok(new
                {
                    summary = new { totalIncome, totalExpense, savings = totalIncome - totalExpense, avgHealth = Math.Round(avgHealth, 0) },
                    weeklySummary = new
                    {
                        weeklyIncome = weekTxns.Where(t => t.Type == "Income").Sum(t => t.Amount),
                        weeklyExpense = weekTxns.Where(t => t.Type == "Expense").Sum(t => t.Amount),
                        weeklyNet = weekTxns.Where(t => t.Type == "Income").Sum(t => t.Amount) - weekTxns.Where(t => t.Type == "Expense").Sum(t => t.Amount),
                        weeklyCount = weekTxns.Count,
                        weekStart = weekStart.ToString("yyyy-MM-dd"),
                        weekEnd = weekStart.AddDays(6).ToString("yyyy-MM-dd")
                    },
                    incomeTrend = new { labels = monthLabels, income = incomeData, expense = expenseData },
                    categoryBreakdown = new { labels = catLabels, values = catValues, colors = catColors },
                    studentActivity = new { labels = activityLabels, data = activityCounts },
                    budgetVsActual = new { labels = budgetLabels, planned = budgetPlanned, actual = budgetActual },
                    savingTrends = new { labels = savingLabels, data = savingTotals },
                    categoryTable,
                    mostUsedCategories = mostUsed,
                    allCategories = allCats,
                    filter = new { fromDate = fromDate.ToString("yyyy-MM-dd"), toDate = toDate.AddDays(-1).ToString("yyyy-MM-dd") }
                });
            }
            catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.Message }); }
        }

        // ============== DASHBOARD ==============
        [HttpGet("/api/admin/dashboard-summary")]
        public async Task<IActionResult> GetDashboardSummary()
        {
            try
            {
                var totalStudents = await _context.Users
                    .Where(u => u.Role != null && u.Role.RoleName == "Student")
                    .CountAsync();

                // Aggregate in DB (do not load all transactions into memory)
                var totalTransactions = await _context.Transactions.CountAsync();
                decimal totalIncome = await _context.Transactions
                    .Where(t => t.Type == "Income")
                    .SumAsync(t => (decimal?)t.Amount) ?? 0m;
                decimal totalExpenses = await _context.Transactions
                    .Where(t => t.Type == "Expense")
                    .SumAsync(t => (decimal?)t.Amount) ?? 0m;

                double avgHealth = 78;
                if (await _context.FinancialHealthScores.AnyAsync())
                {
                    avgHealth = await _context.FinancialHealthScores
                        .AverageAsync(h => (double)h.OverallScore);
                }

                return Ok(new
                {
                    totalStudents,
                    totalTransactions,
                    totalIncome,
                    totalExpenses,
                    systemBalance = totalIncome - totalExpenses,
                    activeBudgets = await _context.Budgets.CountAsync(),
                    savingGoals = await _context.SavingsGoals.CountAsync(g => !g.IsAchieved),
                    avgHealth = Math.Round(avgHealth, 0)
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("GetDashboardSummary error: " + ex);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("/api/admin/recent-transactions")]
        public async Task<IActionResult> GetRecentTransactions()
        {
            try
            {
                var txns = await _context.Transactions.Include(t => t.User).Include(t => t.Category)
                    .OrderByDescending(t => t.CreatedAt).Take(8).ToListAsync();

                var result = txns.Select(t =>
                {
                    var name = t.User?.FullName ?? "Unknown";
                    var initials = string.IsNullOrEmpty(name) ? "??" : string.Join("", name.Split(' ').Take(2).Select(n => n.Length > 0 ? n[0].ToString() : "")).ToUpper();
                    return new
                    {
                        id = t.TransactionId,
                        student = name,
                        initials,
                        category = t.Category?.Name ?? "Other",
                        amount = t.Amount,
                        type = t.Type,
                        status = "Completed"
                    };
                });
                return Ok(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine("GetRecentTransactions error: " + ex);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("/api/admin/charts")]
        public async Task<IActionResult> GetAdminCharts()
        {
            var now = DateTime.UtcNow;
            var currentMonthStart = new DateTime(now.Year, now.Month, 1);

            var labels = new List<string>();
            var incomeData = new List<decimal>();
            var expenseData = new List<decimal>();
            for (int i = 5; i >= 0; i--)
            {
                var mStart = currentMonthStart.AddMonths(-i);
                var mEnd = mStart.AddMonths(1);
                labels.Add(mStart.ToString("MMM"));
                var monthTxns = await _context.Transactions.Where(t => t.Date >= mStart && t.Date < mEnd).ToListAsync();
                incomeData.Add(monthTxns.Where(t => t.Type == "Income").Sum(t => t.Amount));
                expenseData.Add(monthTxns.Where(t => t.Type == "Expense").Sum(t => t.Amount));
            }

            var currentMonthExpenses = await _context.Transactions.Include(t => t.Category)
                .Where(t => t.Type == "Expense" && t.Date >= currentMonthStart).ToListAsync();

            var catGroups = currentMonthExpenses
                .GroupBy(t => t.Category?.Name ?? "Other")
                .Select(g => new { name = g.Key, total = g.Sum(x => x.Amount) })
                .OrderByDescending(x => x.total).Take(6).ToList();

            var palette = new[] { "#38bdf8", "#0ea5e9", "#6366f1", "#818cf8", "#a78bfa", "#22d3ee" };

            var weeklyLabels = new List<string>();
            var weeklyData = new List<int>();
            for (int i = 6; i >= 0; i--)
            {
                var day = now.Date.AddDays(-i);
                var nextDay = day.AddDays(1);
                weeklyLabels.Add(day.ToString("ddd"));
                weeklyData.Add(await _context.Transactions.Where(t => t.Date >= day && t.Date < nextDay).CountAsync());
            }

            var allBudgets = await _context.Budgets.ToListAsync();
            decimal totalBudgetLimit = allBudgets.Sum(b => b.LimitAmount);
            var budgetCategoryIds = allBudgets.Select(b => b.CategoryId).ToList();
            decimal totalBudgetSpent = budgetCategoryIds.Any()
                ? await _context.Transactions.Where(t => t.Type == "Expense" && budgetCategoryIds.Contains(t.CategoryId)).SumAsync(t => t.Amount)
                : 0;
            decimal budgetUsedPercent = totalBudgetLimit > 0 ? Math.Round((totalBudgetSpent / totalBudgetLimit) * 100, 1) : 0;
            if (budgetUsedPercent > 100) budgetUsedPercent = 100;

            var topGoals = await _context.SavingsGoals
                .Where(g => !g.IsAchieved && g.TargetAmount > 0)
                .OrderByDescending(g => g.CurrentAmount).Take(5).ToListAsync();

            var goalLabels = new List<string>();
            var goalData = new List<int>();
            foreach (var goal in topGoals)
            {
                var pct = Math.Min(100, (int)Math.Round((goal.CurrentAmount / goal.TargetAmount) * 100));
                goalLabels.Add(goal.GoalName);
                goalData.Add(pct);
            }
            if (!goalLabels.Any()) { goalLabels.AddRange(new[] { "No Goals", "Yet" }); goalData.AddRange(new[] { 0, 0 }); }

            var goalColors = new List<string>();
            for (int i = 0; i < goalLabels.Count; i++) goalColors.Add(palette[i % palette.Length]);

            return Ok(new
            {
                cashflow = new { labels, incomeData, expenseData },
                expenseCategories = new
                {
                    labels = catGroups.Select(c => c.name).ToList(),
                    values = catGroups.Select(c => (int)c.total).ToList(),
                    colors = palette.Take(catGroups.Count).ToList()
                },
                weeklyActivity = new { labels = weeklyLabels, data = weeklyData },
                budgetUsage = new { used = budgetUsedPercent, remaining = 100 - budgetUsedPercent, totalLimit = totalBudgetLimit, totalSpent = totalBudgetSpent },
                savingGoals = new { labels = goalLabels, data = goalData, colors = goalColors }
            });
        }

        [HttpGet("/api/admin/stats")]
        public async Task<IActionResult> GetSystemStats()
        {
            int totalUsers = await _context.Users.CountAsync();
            int activeUsers = await _context.Users.CountAsync(u => u.IsActive);
            int totalTransactions = await _context.Transactions.CountAsync();
            decimal totalVolume = await _context.Transactions.SumAsync(t => t.Amount);

            var topCategories = await _context.Transactions.Include(t => t.Category)
                .GroupBy(t => t.Category != null ? t.Category.Name : "Other")
                .Select(g => new { Name = g.Key, Count = g.Count(), Volume = g.Sum(x => x.Amount) })
                .OrderByDescending(x => x.Count).Take(5).ToListAsync();

            return Ok(new { totalUsers, activeUsers, totalTransactions, totalVolume, topCategories });
        }

        [HttpGet("/api/admin/users")]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _context.Users.Include(u => u.Role)
                .Select(u => new
                {
                    u.UserId,
                    u.FullName,
                    u.Email,
                    Role = u.Role != null ? u.Role.RoleName : "Student",
                    u.AcademicYear,
                    u.IsActive,
                    u.CreatedAt,
                    TransactionCount = u.Transactions.Count,
                    TotalSpent = u.Transactions.Where(t => t.Type == "Expense").Sum(t => t.Amount)
                })
                .OrderByDescending(u => u.CreatedAt).ToListAsync();
            return Ok(users);
        }

        [HttpPut("/api/admin/users/{id}/toggle-status")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ToggleUserStatus(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            user.IsActive = !user.IsActive;
            await _context.SaveChangesAsync();
            return Ok(new { success = true, isActive = user.IsActive });
        }

        [HttpPut("/api/admin/users/{id}/reset-password")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ResetUserPassword(int id, [FromBody] ResetUserPasswordRequest model)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            bool generatedPassword = string.IsNullOrWhiteSpace(model.NewPassword);
            string newPass = generatedPassword ? GenerateTemporaryPassword() : model.NewPassword.Trim();
            user.PasswordHash = _passwordHasher.HashPassword(user, newPass);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = $"Password for {user.Email} has been reset.", temporaryPassword = newPass });
        }

        // ============== ANNOUNCEMENTS ==============
        [HttpGet("/api/admin/announcements")]
        public async Task<IActionResult> GetAnnouncements()
        {
            var list = await _context.Announcements.Include(a => a.PostedByAdmin)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new
                {
                    a.AnnouncementId,
                    a.Title,
                    a.Message,
                    PostedBy = a.PostedByAdmin != null ? a.PostedByAdmin.FullName : "Admin",
                    a.CreatedAt
                }).ToListAsync();
            return Ok(list);
        }

        [HttpPost("/api/admin/announcements")]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateAnnouncement([FromBody] CreateAnnouncementRequest model)
        {
            if (string.IsNullOrWhiteSpace(model.Title) || string.IsNullOrWhiteSpace(model.Message))
                return BadRequest(new { success = false, message = "Title and message are required." });

            int adminId = GetCurrentUserId();
            var announcement = new Announcement
            {
                Title = model.Title.Trim(),
                Message = model.Message.Trim(),
                PostedByAdminId = adminId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _context.Announcements.Add(announcement);

            var students = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.IsActive && u.Role != null && u.Role.RoleName == "Student")
                .ToListAsync();
            foreach (var student in students)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = student.UserId,
                    Message = $"📢 Announcement: {announcement.Title} — {announcement.Message}",
                    Type = "Info",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });
            }
            await _context.SaveChangesAsync();
            return Ok(new { success = true, announcementId = announcement.AnnouncementId });
        }

        [HttpDelete("/api/admin/announcements/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteAnnouncement(int id)
        {
            var ann = await _context.Announcements.FindAsync(id);
            if (ann == null) return NotFound();
            _context.Announcements.Remove(ann);
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        private static string SanitizeEventImage(string? image)
        {
            const string defaultImage = "https://images.unsplash.com/photo-1540575467063-178a50c2df87?w=500";
            var img = (image ?? "").Trim();
            if (string.IsNullOrWhiteSpace(img)) return defaultImage;
            if (img.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return defaultImage;
            if (img.Length > 1000) return defaultImage;
            if (!(img.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                  img.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                return defaultImage;
            return img;
        }
    }

    // ============== REQUEST DTOs ==============

    public class ResetUserPasswordRequest { public string? NewPassword { get; set; } }

    public class AddDefaultCategoryRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "Expense";
    }

    public class CreateAnnouncementRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class StudentUpsertRequest
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Department { get; set; } = "General";
        public string Status { get; set; } = "Active";
        public int Health { get; set; } = 80;
        public string? Password { get; set; }
    }

    public class BulkStudentRequest { public List<int> Ids { get; set; } = new(); }

    public class BudgetUpsertRequest
    {
        public int UserId { get; set; }
        public int CategoryId { get; set; }
        public decimal Limit { get; set; }
    }

    public class SavingGoalUpsertRequest
    {
        public int UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public decimal Target { get; set; }
        public decimal Saved { get; set; }
        public DateTime Date { get; set; }
    }

    public class DepositRequest { public decimal Amount { get; set; } }

    public class AdminProfileRequest
    {
        public string? FullName { get; set; }
        public string? Email { get; set; }
    }

    public class AdminPasswordRequest
    {
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class AdminTransactionUpsertRequest
    {
        public decimal Amount { get; set; }
        public string? Type { get; set; }
        public int? CategoryId { get; set; }
        public DateTime? Date { get; set; }
    }

    public class EventCreateRequest
    {
        public string Title { get; set; } = string.Empty;
        public string? Category { get; set; }
        public DateTime EventDate { get; set; }
        public string? Time { get; set; }
        public string? Venue { get; set; }
        public decimal Fee { get; set; }
        public int SeatsTotal { get; set; } = 100;
        public string? Description { get; set; }
        public string? Image { get; set; }
        public bool AutoApprove { get; set; } = true;
        public bool WalletIntegration { get; set; } = true;
    }

    public class VoucherCreateRequest
    {
        public string StudentName { get; set; } = string.Empty;
        public string? Department { get; set; }
        public string? Title { get; set; }
        public string? Semester { get; set; }
        public decimal Tuition { get; set; }
        public decimal Discount { get; set; }
        public decimal Fine { get; set; }
        public DateTime DueDate { get; set; }
        public string? Status { get; set; }
    }

    public class BulkVoucherRequest
    {
        public string Department { get; set; } = string.Empty;
        public string Batch { get; set; } = string.Empty;
        public DateTime DueDate { get; set; }
        public decimal Tuition { get; set; }
    }

    public class FeeConfigRequest
    {
        public decimal Tuition { get; set; }
        public decimal Lab { get; set; }
        public decimal Admission { get; set; }
        public decimal Fine { get; set; }
    }

    public class CmsContentUpdate
    {
        public string Value { get; set; } = string.Empty;
    }

    public class AdminAlertNotifyRequest
    {
        public int UserId { get; set; }
        public string? Message { get; set; }
    }

    public class AdminAlertBudgetUpdate
    {
        public decimal LimitAmount { get; set; }
    }
}
