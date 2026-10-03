using CampusCoin.Data;
using CampusCoin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CampusCoin.Controllers
{
    [Authorize(Roles = "Student")]
    public class FeeVouchersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public FeeVouchersController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out int id) ? id : 0;
        }

        [HttpGet("/FeeVouchers")]
        public IActionResult Index() => View();

        [HttpGet("/api/feevouchers")]
        public async Task<IActionResult> GetVouchers()
        {
            int userId = GetCurrentUserId();
            var today = DateTime.UtcNow;
            var vouchers = await _context.FeeVouchers
                .Where(v => v.UserId == userId)
                .OrderByDescending(v => v.DueDate)
                .ToListAsync();

            // Auto-update overdue + fine
            bool changed = false;
            foreach (var v in vouchers)
            {
                if (v.Status != "Paid" && v.DueDate < today)
                {
                    if (v.Status != "Overdue") { v.Status = "Overdue"; changed = true; }
                    var days = (int)(today - v.DueDate).TotalDays;
                    var newFine = 300 + (days * 100);
                    if (v.Fine != newFine) { v.Fine = newFine; changed = true; }
                }
            }
            if (changed) await _context.SaveChangesAsync();

            return Ok(vouchers.Select(v => new {
                id = v.Id,
                voucherCode = v.VoucherCode,
                title = v.Title,
                semester = v.Semester,
                amount = v.Amount,
                fine = v.Fine,
                issueDate = v.IssueDate.ToString("yyyy-MM-dd"),
                dueDate = v.DueDate.ToString("yyyy-MM-dd"),
                status = v.Status,
                paidAt = v.PaidAt,
                paymentMethod = v.PaymentMethod
            }));
        }

        [HttpGet("/api/feevouchers/summary")]
        public async Task<IActionResult> GetSummary()
        {
            int userId = GetCurrentUserId();
            var all = await _context.FeeVouchers.Where(v => v.UserId == userId).ToListAsync();
            var user = await _context.Users.FindAsync(userId);

            var totalDue = all.Where(v => v.Status != "Paid").Sum(v => v.Amount + v.Fine);
            var totalFine = all.Sum(v => v.Fine);
            var nextDue = all.Where(v => v.Status != "Paid").OrderBy(v => v.DueDate).FirstOrDefault();

            return Ok(new
            {
                totalDue,
                totalFine,
                walletBalance = user?.MonthlyAllowanceBaseline ?? 0,
                upcomingDueDate = nextDue?.DueDate.ToString("yyyy-MM-dd"),
                upcomingTitle = nextDue?.Title,
                paidCount = all.Count(v => v.Status == "Paid"),
                pendingCount = all.Count(v => v.Status == "Pending"),
                overdueCount = all.Count(v => v.Status == "Overdue")
            });
        }

        [HttpPost("/api/feevouchers/{id}/pay")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> PayVoucher(int id, [FromBody] PayRequest model)
        {
            try
            {
                int userId = GetCurrentUserId();
                var voucher = await _context.FeeVouchers
                    .FirstOrDefaultAsync(v => v.Id == id && v.UserId == userId);

                if (voucher == null)
                    return NotFound(new { success = false, message = "Voucher not found" });
                if (voucher.Status == "Paid")
                    return BadRequest(new { success = false, message = "Already paid" });

                var total = voucher.Amount + voucher.Fine;

                if (model.PaymentMethod == "Wallet")
                {
                    var user = await _context.Users.FindAsync(userId);
                    if (user != null && user.MonthlyAllowanceBaseline < total)
                        return BadRequest(new { success = false, message = "Insufficient wallet balance" });
                    if (user != null) user.MonthlyAllowanceBaseline -= total;
                }

                voucher.Status = "Paid";
                voucher.Fine = 0;
                voucher.PaidAt = DateTime.UtcNow;
                voucher.PaymentMethod = model.PaymentMethod;
                voucher.TransactionRef = model.TransactionRef;

                // Resolve a valid expense category for the fee transaction
                var expenseCats = await _context.Categories
                    .Where(c => (c.UserId == null || c.UserId == userId) && c.Type == "Expense")
                    .ToListAsync();
                var feeCat = expenseCats.FirstOrDefault(c =>
                        c.Name.Contains("Fee", StringComparison.OrdinalIgnoreCase)
                        || c.Name.Contains("University", StringComparison.OrdinalIgnoreCase)
                        || c.Name.Contains("Tuition", StringComparison.OrdinalIgnoreCase))
                    ?? expenseCats.OrderBy(c => c.CategoryId).FirstOrDefault();
                if (feeCat == null)
                {
                    feeCat = new Category { Name = "University Fees", Type = "Expense", IsDefault = true, UserId = null };
                    _context.Categories.Add(feeCat);
                    await _context.SaveChangesAsync();
                }

                _context.Transactions.Add(new Transaction
                {
                    UserId = userId,
                    CategoryId = feeCat.CategoryId,
                    Amount = total,
                    Type = "Expense",
                    Description = $"Fee: {voucher.Title}",
                    Date = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Payment successful" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }

    public class PayRequest
    {
        public string PaymentMethod { get; set; } = "Wallet";
        public string? TransactionRef { get; set; }
    }
}