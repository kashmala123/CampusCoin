using CampusCoin.Data;
using CampusCoin.Models;
using CampusCoin.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace CampusCoin.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            ApplicationDbContext context,
            IEmailService emailService,
            IConfiguration configuration,
            ILogger<HomeController> logger)
        {
            _context = context;
            _emailService = emailService;
            _configuration = configuration;
            _logger = logger;
        }

        // ================= GET: / =================
        public async Task<IActionResult> Index()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var role = User.FindFirst(ClaimTypes.Role)?.Value;
                return role == "Admin"
                    ? RedirectToAction("Index", "Admin")
                    : RedirectToAction("Index", "Dashboard");
            }

            var vm = new HomepageViewModel();
            try
            {
                vm.Content = await _context.SiteContents
                    .AsNoTracking()
                    .ToDictionaryAsync(x => x.ContentKey, x => x.Value);

                vm.Testimonials = await _context.HomepageTestimonials
                    .AsNoTracking()
                    .Where(x => x.IsActive)
                    .OrderBy(x => x.SortOrder)
                    .ToListAsync();

                vm.Features = await _context.HomepageFeatures
                    .AsNoTracking()
                    .Where(x => x.IsActive)
                    .OrderBy(x => x.SortOrder)
                    .ToListAsync();

                var cats = await _context.HomepageCategoryItems
                    .AsNoTracking()
                    .Where(x => x.IsActive)
                    .OrderBy(x => x.SortOrder)
                    .ToListAsync();
                vm.IncomeItems = cats.Where(c => c.Group == "Income").ToList();
                vm.ExpenseItems = cats.Where(c => c.Group == "Expense").ToList();

                vm.Proofs = await _context.HomepageProofs
                    .AsNoTracking()
                    .Where(x => x.IsActive)
                    .OrderBy(x => x.SortOrder)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Homepage CMS load failed — using empty/defaults.");
            }

            return View(vm);
        }

        public IActionResult Privacy() => View();
        public IActionResult Terms() => View();
        public IActionResult Cookies() => View();

        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Error()
        {
            Response.StatusCode = 500;
            ViewData["Title"] = "Something went wrong";
            return View("NotFound");
        }

        public IActionResult NotFound(int? code = null)
        {
            Response.StatusCode = code ?? 404;
            ViewData["Title"] = "Page not found";
            return View();
        }

        // ================= POST: /Home/Contact =================
        [HttpPost]
        public async Task<IActionResult> Contact([FromBody] ContactRequest model)
        {
            if (model == null)
                return BadRequest(new { success = false, message = "Invalid request." });

            var name = (model.Name ?? "").Trim();
            var email = (model.Email ?? "").Trim();
            var subject = (model.Subject ?? "").Trim();
            var message = (model.Message ?? "").Trim();
            var topic = (model.Topic ?? "General").Trim();

            if (string.IsNullOrWhiteSpace(name) || name.Length < 2)
                return BadRequest(new { success = false, message = "Please enter your name." });

            // Letters and spaces only (matches client-side rule)
            if (!Regex.IsMatch(name, @"^[A-Za-zÀ-ÖØ-öø-ÿ\s'.-]{2,80}$"))
                return BadRequest(new { success = false, message = "Name can only contain letters and spaces." });

            if (string.IsNullOrWhiteSpace(email) || !IsValidEmail(email))
                return BadRequest(new { success = false, message = "Please enter a valid email address." });

            if (string.IsNullOrWhiteSpace(subject) || subject.Length < 3)
                return BadRequest(new { success = false, message = "Please enter a subject." });

            if (string.IsNullOrWhiteSpace(message) || message.Length < 10)
                return BadRequest(new { success = false, message = "Message should be at least 10 characters." });

            if (message.Length > 500)
                return BadRequest(new { success = false, message = "Message cannot exceed 500 characters." });

            var adminInbox = _configuration["Email:FromEmail"] ?? "";
            var safeName = System.Net.WebUtility.HtmlEncode(name);
            var safeEmail = System.Net.WebUtility.HtmlEncode(email);
            var safeSubject = System.Net.WebUtility.HtmlEncode(subject);
            var safeTopic = System.Net.WebUtility.HtmlEncode(topic);
            var safeMessage = System.Net.WebUtility.HtmlEncode(message).Replace("\n", "<br/>");

            var adminHtml = $@"
<div style=""font-family:Segoe UI,Arial,sans-serif;max-width:600px;margin:0 auto;padding:20px;"">
  <h2 style=""color:#4f46e5;margin:0 0 12px;"">New Campus Coin contact message</h2>
  <p style=""margin:0 0 8px;""><strong>Topic:</strong> {safeTopic}</p>
  <p style=""margin:0 0 8px;""><strong>From:</strong> {safeName} &lt;{safeEmail}&gt;</p>
  <p style=""margin:0 0 8px;""><strong>Subject:</strong> {safeSubject}</p>
  <hr style=""border:none;border-top:1px solid #e2e8f0;margin:16px 0;""/>
  <p style=""color:#334155;line-height:1.6;"">{safeMessage}</p>
</div>";

            var (sent, err) = await _emailService.SendAsync(
                adminInbox,
                $"[Campus Coin] {topic}: {subject}",
                adminHtml,
                replyTo: email);

            if (!sent)
            {
                _logger.LogError("Contact form email failed: {Err}", err);
                return StatusCode(500, new { success = false, message = "Could not send your message right now. Please try again in a moment." });
            }

            // Friendly auto-reply to the student
            var firstName = name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? name;
            var replyHtml = $@"
<div style=""font-family:Segoe UI,Arial,sans-serif;max-width:560px;margin:0 auto;padding:24px;background:#f8fafc;border-radius:16px;"">
  <div style=""background:linear-gradient(135deg,#0ea5e9,#6366f1);padding:18px 22px;border-radius:12px 12px 0 0;color:#fff;"">
    <h2 style=""margin:0;font-size:18px;"">Thanks for reaching out!</h2>
  </div>
  <div style=""background:#fff;padding:22px;border-radius:0 0 12px 12px;border:1px solid #e2e8f0;"">
    <p style=""margin:0 0 12px;color:#0f172a;"">Hi {System.Net.WebUtility.HtmlEncode(firstName)},</p>
    <p style=""margin:0 0 12px;color:#334155;line-height:1.55;"">We received your message about <strong>{safeSubject}</strong> and our team will get back to you soon — usually within a day.</p>
    <p style=""margin:0;color:#94a3b8;font-size:12px;"">— The Campus Coin team</p>
  </div>
</div>";
            await _emailService.SendAsync(email, "We got your message — Campus Coin", replyHtml);

            return Ok(new { success = true, message = "Message sent! We'll get back to you soon." });
        }

        // ================= POST: /Home/Newsletter =================
        [HttpPost]
        public async Task<IActionResult> Newsletter([FromBody] NewsletterRequest model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Email))
                return BadRequest(new { success = false, message = "Please enter your email address." });

            var email = model.Email.Trim();
            if (!IsValidEmail(email))
                return BadRequest(new { success = false, message = "Please enter a valid email address." });

            var html = $@"
<div style=""font-family:Segoe UI,Arial,sans-serif;max-width:560px;margin:0 auto;padding:24px;background:#f8fafc;border-radius:16px;"">
  <div style=""background:linear-gradient(135deg,#0ea5e9,#6366f1);padding:18px 22px;border-radius:12px 12px 0 0;color:#fff;"">
    <h2 style=""margin:0;font-size:18px;"">You're on the list ✨</h2>
  </div>
  <div style=""background:#fff;padding:22px;border-radius:0 0 12px 12px;border:1px solid #e2e8f0;"">
    <p style=""margin:0 0 12px;color:#0f172a;"">Welcome to Campus Coin updates.</p>
    <p style=""margin:0 0 12px;color:#334155;line-height:1.55;"">We'll share student-friendly saving tips, product news, and campus finance ideas — no spam, unsubscribe anytime.</p>
    <p style=""margin:0;color:#94a3b8;font-size:12px;"">— The Campus Coin team</p>
  </div>
</div>";

            var (sent, err) = await _emailService.SendAsync(email, "Subscribed to Campus Coin updates", html);
            if (!sent)
            {
                _logger.LogError("Newsletter email failed for {Email}: {Err}", email, err);
                return StatusCode(500, new { success = false, message = "Could not complete subscription right now. Please try again." });
            }

            // Notify admin of new subscriber (optional, best-effort)
            var adminInbox = _configuration["Email:FromEmail"];
            if (!string.IsNullOrWhiteSpace(adminInbox))
            {
                await _emailService.SendAsync(
                    adminInbox,
                    "New newsletter subscriber",
                    $"<p>New subscriber: <strong>{System.Net.WebUtility.HtmlEncode(email)}</strong></p>");
            }

            return Ok(new { success = true, message = "Subscribed — check your inbox ✨" });
        }

        private static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email) || email.Length > 150) return false;
            return Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);
        }
    }

    public class ContactRequest
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Subject { get; set; }
        public string? Message { get; set; }
        public string? Topic { get; set; }
    }

    public class NewsletterRequest
    {
        public string? Email { get; set; }
    }
}
