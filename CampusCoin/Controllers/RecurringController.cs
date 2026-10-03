using CampusCoin.Data;
using CampusCoin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CampusCoin.Controllers
{
    [Authorize(Roles = "Student")]
    [ApiController]
    [Route("api/recurring")]
    public class RecurringController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public RecurringController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out int id) ? id : 0;
        }

        // GET: /api/recurring
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            int userId = GetCurrentUserId();

            var items = await _context.RecurringTransactions
                .Include(r => r.Category)
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.IsActive)
                .ThenBy(r => r.NextRunDate)
                .ToListAsync();

            var result = items.Select(r => new
            {
                recurringId = r.RecurringId,
                categoryId = r.CategoryId,
                categoryName = r.Category?.Name ?? "Unknown",
                amount = r.Amount,
                type = r.Type,
                description = r.Description,
                frequency = r.Frequency,
                nextRunDate = r.NextRunDate.ToString("yyyy-MM-dd"),
                isActive = r.IsActive,
                createdAt = r.CreatedAt.ToString("MMM dd, yyyy")
            });

            return Ok(result);
        }

        // POST: /api/recurring
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] RecurringDto model)
        {
            if (model == null || model.Amount <= 0)
                return BadRequest(new { message = "Invalid data." });

            int userId = GetCurrentUserId();

            var category = await _context.Categories.FindAsync(model.CategoryId);
            if (category == null)
                return BadRequest(new { message = "Category not found." });

            var recurring = new RecurringTransaction
            {
                UserId = userId,
                CategoryId = model.CategoryId,
                Amount = model.Amount,
                Type = model.Type,
                Description = model.Description ?? category.Name,
                Frequency = model.Frequency,
                NextRunDate = model.NextRunDate,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.RecurringTransactions.Add(recurring);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Recurring transaction created!", recurringId = recurring.RecurringId });
        }

        // PUT: /api/recurring/{id}/toggle
        [HttpPut("{id}/toggle")]
        public async Task<IActionResult> Toggle(int id)
        {
            int userId = GetCurrentUserId();
            var item = await _context.RecurringTransactions
                .FirstOrDefaultAsync(r => r.RecurringId == id && r.UserId == userId);

            if (item == null) return NotFound(new { message = "Not found." });

            item.IsActive = !item.IsActive;
            await _context.SaveChangesAsync();

            return Ok(new { message = item.IsActive ? "Activated" : "Paused", isActive = item.IsActive });
        }

        // DELETE: /api/recurring/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            int userId = GetCurrentUserId();
            var item = await _context.RecurringTransactions
                .FirstOrDefaultAsync(r => r.RecurringId == id && r.UserId == userId);

            if (item == null) return NotFound(new { message = "Not found." });

            _context.RecurringTransactions.Remove(item);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Deleted." });
        }

        // POST: /api/recurring/process-due
        // Manually process all due recurring transactions
        [HttpPost("process-due")]
        public async Task<IActionResult> ProcessDue()
        {
            int userId = GetCurrentUserId();
            var today = DateTime.UtcNow.Date;

            var dueItems = await _context.RecurringTransactions
                .Where(r => r.UserId == userId && r.IsActive && r.NextRunDate <= today)
                .ToListAsync();

            int created = 0;

            foreach (var item in dueItems)
            {
                // Create transaction
                var txn = new Transaction
                {
                    UserId = userId,
                    CategoryId = item.CategoryId,
                    Amount = item.Amount,
                    Type = item.Type,
                    Description = item.Description,
                    Date = item.NextRunDate,
                    IsRecurring = true,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Transactions.Add(txn);

                // Advance NextRunDate
                item.NextRunDate = item.Frequency switch
                {
                    "Weekly" => item.NextRunDate.AddDays(7),
                    "Monthly" => item.NextRunDate.AddMonths(1),
                    "Yearly" => item.NextRunDate.AddYears(1),
                    _ => item.NextRunDate.AddMonths(1)
                };

                created++;
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = $"{created} recurring transactions processed!", count = created });
        }
    }

    public class RecurringDto
    {
        public int CategoryId { get; set; }
        public decimal Amount { get; set; }
        public string Type { get; set; } = "Expense";
        public string? Description { get; set; }
        public string Frequency { get; set; } = "Monthly";
        public DateTime NextRunDate { get; set; }
    }
}