using CampusCoin.Data;
using CampusCoin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;

namespace CampusCoin.Controllers
{
    /// <summary>
    /// Shared / roommate bill split tracking for students.
    /// </summary>
    [Authorize(Roles = "Student")]
    [Route("api/shared-expenses")]
    [ApiController]
    public class SharedExpensesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public SharedExpensesController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out int id) ? id : 0;
        }

        // GET /api/shared-expenses?filter=all|open|settled
        [HttpGet]
        public async Task<IActionResult> List([FromQuery] string? filter = "all")
        {
            int userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var list = await _context.SharedExpenses
                .Include(s => s.Transaction)
                .Where(s => s.PayerUserId == userId)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            var ids = list.Select(s => s.SharedExpenseId).ToList();
            var splits = await _context.SharedExpenseSplits
                .Where(x => ids.Contains(x.SharedExpenseId))
                .ToListAsync();

            var result = list.Select(s =>
            {
                var parts = splits.Where(p => p.SharedExpenseId == s.SharedExpenseId).ToList();
                var owed = parts.Where(p => !p.IsSettled).Sum(p => p.ShareAmount);
                var settledAmt = parts.Where(p => p.IsSettled).Sum(p => p.ShareAmount);
                var allSettled = parts.Count > 0 && parts.All(p => p.IsSettled);
                return new
                {
                    id = s.SharedExpenseId,
                    description = s.Description,
                    totalAmount = s.TotalAmount,
                    createdAt = s.CreatedAt,
                    transactionId = s.TransactionId,
                    participants = parts.Select(p => new
                    {
                        splitId = p.SplitId,
                        name = p.ParticipantName,
                        shareAmount = p.ShareAmount,
                        isSettled = p.IsSettled
                    }),
                    totalOwedToYou = owed,
                    totalSettled = settledAmt,
                    allSettled,
                    participantCount = parts.Count
                };
            }).ToList();

            var f = (filter ?? "all").Trim().ToLowerInvariant();
            if (f == "open") result = result.Where(r => !r.allSettled).ToList();
            else if (f == "settled") result = result.Where(r => r.allSettled).ToList();

            decimal openOwed = list
                .SelectMany(s => splits.Where(p => p.SharedExpenseId == s.SharedExpenseId && !p.IsSettled))
                .Sum(p => p.ShareAmount);

            int openCount = list.Count(s =>
            {
                var parts = splits.Where(p => p.SharedExpenseId == s.SharedExpenseId).ToList();
                return parts.Count > 0 && parts.Any(p => !p.IsSettled);
            });

            return Ok(new
            {
                success = true,
                summary = new
                {
                    openSplits = openCount,
                    totalOwedToYou = openOwed,
                    totalSplits = list.Count,
                    settledSplits = list.Count - openCount
                },
                items = result
            });
        }

        // POST /api/shared-expenses
        // includeMe: if true, total is divided by (roommates + you); only roommates appear as owing you
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSharedExpenseRequest? model)
        {
            int userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();
            if (model == null || string.IsNullOrWhiteSpace(model.Description))
                return BadRequest(new { success = false, message = "Description is required." });
            if (model.TotalAmount <= 0)
                return BadRequest(new { success = false, message = "Total amount must be greater than zero." });
            if (model.Participants == null || model.Participants.Count == 0)
                return BadRequest(new { success = false, message = "Add at least one roommate/friend." });

            var names = model.Participants
                .Where(p => !string.IsNullOrWhiteSpace(p.Name))
                .Select(p => p.Name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (names.Count == 0)
                return BadRequest(new { success = false, message = "Participant names are required." });

            bool includeMe = model.IncludeMe;
            bool equalSplit = model.EqualSplit || model.Participants.All(p => !p.ShareAmount.HasValue || p.ShareAmount <= 0);

            var shares = new List<(string Name, decimal Amount)>();
            decimal yourShare = 0m;

            if (equalSplit)
            {
                int heads = includeMe ? names.Count + 1 : names.Count;
                if (heads <= 0) heads = 1;
                decimal each = Math.Round(model.TotalAmount / heads, 2, MidpointRounding.AwayFromZero);
                decimal allocated = 0;
                for (int i = 0; i < names.Count; i++)
                {
                    decimal amt = (i == names.Count - 1 && !includeMe)
                        ? (model.TotalAmount - allocated)
                        : each;
                    if (i == names.Count - 1 && includeMe)
                    {
                        // leave remainder for "you"
                        amt = each;
                    }
                    shares.Add((names[i], amt));
                    allocated += amt;
                }
                if (includeMe)
                {
                    yourShare = model.TotalAmount - allocated;
                    if (yourShare < 0) yourShare = 0;
                }
            }
            else
            {
                foreach (var p in model.Participants.Where(p => !string.IsNullOrWhiteSpace(p.Name)))
                {
                    var amt = p.ShareAmount ?? 0;
                    if (amt <= 0) continue;
                    shares.Add((p.Name.Trim(), amt));
                }
                if (shares.Count == 0)
                    return BadRequest(new { success = false, message = "Enter valid share amounts." });

                var sum = shares.Sum(s => s.Amount);
                if (includeMe)
                {
                    if (sum > model.TotalAmount)
                        return BadRequest(new { success = false, message = $"Roommate shares ({sum:N2}) cannot exceed total ({model.TotalAmount:N2})." });
                    yourShare = model.TotalAmount - sum;
                }
                else if (Math.Abs(sum - model.TotalAmount) > 0.05m)
                {
                    return BadRequest(new { success = false, message = $"Shares ({sum:N2}) must add up to total ({model.TotalAmount:N2})." });
                }
            }

            Transaction? txn = null;
            if (model.ExistingTransactionId.HasValue && model.ExistingTransactionId.Value > 0)
            {
                txn = await _context.Transactions.FirstOrDefaultAsync(t =>
                    t.TransactionId == model.ExistingTransactionId.Value && t.UserId == userId && t.Type == "Expense");
                if (txn == null)
                    return BadRequest(new { success = false, message = "Selected transaction not found." });
                // already linked?
                if (await _context.SharedExpenses.AnyAsync(s => s.TransactionId == txn.TransactionId))
                    return BadRequest(new { success = false, message = "This transaction is already linked to a bill split." });
            }
            else
            {
                var cat = await _context.Categories
                    .FirstOrDefaultAsync(c => c.Type == "Expense" && (c.UserId == null || c.UserId == userId) &&
                        (c.Name == "Miscellaneous" || c.Name == "Food" || c.Name == "Hostel/Rent"));
                if (cat == null)
                {
                    cat = new Category { Name = "Miscellaneous", Type = "Expense", IsDefault = false, UserId = userId };
                    _context.Categories.Add(cat);
                    await _context.SaveChangesAsync();
                }

                var note = includeMe
                    ? $"[Split] {model.Description.Trim()} (your share ~ {yourShare:N2}; others owe you)"
                    : $"[Split] {model.Description.Trim()} (fully recoverable from roommates)";

                txn = new Transaction
                {
                    UserId = userId,
                    CategoryId = cat.CategoryId,
                    Amount = model.TotalAmount,
                    Type = "Expense",
                    Description = note,
                    Date = DateTime.UtcNow.Date,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Transactions.Add(txn);
                await _context.SaveChangesAsync();
            }

            var shared = new SharedExpense
            {
                TransactionId = txn.TransactionId,
                PayerUserId = userId,
                TotalAmount = model.TotalAmount,
                Description = model.Description.Trim(),
                CreatedAt = DateTime.UtcNow
            };
            _context.SharedExpenses.Add(shared);
            await _context.SaveChangesAsync();

            foreach (var (name, amount) in shares)
            {
                _context.SharedExpenseSplits.Add(new SharedExpenseSplit
                {
                    SharedExpenseId = shared.SharedExpenseId,
                    ParticipantName = name,
                    ShareAmount = amount,
                    IsSettled = false
                });
            }

            _context.Notifications.Add(new Notification
            {
                UserId = userId,
                Message = $"Bill split created: \"{model.Description.Trim()}\" ({model.TotalAmount:N2}). {shares.Count} person(s) tracked in Bill Split.",
                Type = "Info",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = includeMe
                    ? $"Split created. Each person ~ equal share; your part is {yourShare:N2}. Track who pays you back below."
                    : "Split created. Full amount is tracked as recoverable from roommates.",
                sharedExpenseId = shared.SharedExpenseId,
                transactionId = txn.TransactionId,
                yourShare,
                recoverable = shares.Sum(s => s.Amount)
            });
        }


        // PUT /api/shared-expenses/{id} — edit description & participants (easy to understand)
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateSharedExpenseRequest? model)
        {
            int userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();
            if (model == null || string.IsNullOrWhiteSpace(model.Description))
                return BadRequest(new { success = false, message = "Description is required." });
            if (model.TotalAmount <= 0)
                return BadRequest(new { success = false, message = "Total amount must be greater than zero." });
            if (model.Participants == null || model.Participants.Count == 0)
                return BadRequest(new { success = false, message = "Add at least one roommate." });

            var shared = await _context.SharedExpenses.FirstOrDefaultAsync(s => s.SharedExpenseId == id && s.PayerUserId == userId);
            if (shared == null) return NotFound(new { success = false, message = "Split not found." });

            var names = model.Participants
                .Where(p => !string.IsNullOrWhiteSpace(p.Name))
                .Select(p => p.Name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (names.Count == 0)
                return BadRequest(new { success = false, message = "Participant names are required." });

            bool includeMe = model.IncludeMe;
            bool equalSplit = model.EqualSplit || model.Participants.All(p => !p.ShareAmount.HasValue || p.ShareAmount <= 0);

            var shares = new List<(string Name, decimal Amount)>();
            if (equalSplit)
            {
                int heads = includeMe ? names.Count + 1 : names.Count;
                if (heads <= 0) heads = 1;
                decimal each = Math.Round(model.TotalAmount / heads, 2, MidpointRounding.AwayFromZero);
                decimal allocated = 0;
                for (int i = 0; i < names.Count; i++)
                {
                    decimal amt = each;
                    if (i == names.Count - 1 && !includeMe)
                        amt = model.TotalAmount - allocated;
                    shares.Add((names[i], amt));
                    allocated += amt;
                }
            }
            else
            {
                foreach (var p in model.Participants.Where(p => !string.IsNullOrWhiteSpace(p.Name)))
                {
                    var amt = p.ShareAmount ?? 0;
                    if (amt <= 0) continue;
                    shares.Add((p.Name.Trim(), amt));
                }
                if (shares.Count == 0)
                    return BadRequest(new { success = false, message = "Enter valid share amounts." });
                var sum = shares.Sum(s => s.Amount);
                if (!includeMe && Math.Abs(sum - model.TotalAmount) > 0.05m)
                    return BadRequest(new { success = false, message = $"Shares ({sum:N2}) must add up to total ({model.TotalAmount:N2})." });
                if (includeMe && sum > model.TotalAmount)
                    return BadRequest(new { success = false, message = "Roommate shares cannot exceed total." });
            }

            // Remember settled state by name
            var oldSplits = await _context.SharedExpenseSplits.Where(s => s.SharedExpenseId == id).ToListAsync();
            var settledMap = oldSplits
                .GroupBy(s => s.ParticipantName.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Any(x => x.IsSettled), StringComparer.OrdinalIgnoreCase);

            _context.SharedExpenseSplits.RemoveRange(oldSplits);

            shared.Description = model.Description.Trim();
            shared.TotalAmount = model.TotalAmount;

            // Keep linked transaction in sync
            var txn = await _context.Transactions.FirstOrDefaultAsync(t => t.TransactionId == shared.TransactionId && t.UserId == userId);
            if (txn != null)
            {
                txn.Amount = model.TotalAmount;
                txn.Description = $"[Split] {model.Description.Trim()}";
            }

            foreach (var (name, amount) in shares)
            {
                settledMap.TryGetValue(name, out bool wasSettled);
                _context.SharedExpenseSplits.Add(new SharedExpenseSplit
                {
                    SharedExpenseId = shared.SharedExpenseId,
                    ParticipantName = name,
                    ShareAmount = amount,
                    IsSettled = wasSettled
                });
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Split updated." });
        }


        // GET /api/shared-expenses/{id}/summary-text  — for WhatsApp/copy
        [HttpGet("{id:int}/summary-text")]
        public async Task<IActionResult> SummaryText(int id)
        {
            int userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var shared = await _context.SharedExpenses.FirstOrDefaultAsync(s => s.SharedExpenseId == id && s.PayerUserId == userId);
            if (shared == null) return NotFound(new { success = false, message = "Not found." });

            var parts = await _context.SharedExpenseSplits.Where(s => s.SharedExpenseId == id).ToListAsync();
            var user = await _context.Users.FindAsync(userId);

            var sb = new StringBuilder();
            sb.AppendLine($"CampusCoin bill split — {shared.Description}");
            sb.AppendLine($"Paid by: {user?.FullName ?? "me"} · Total: {shared.TotalAmount:N2}");
            sb.AppendLine("-----------");
            foreach (var p in parts)
            {
                sb.AppendLine($"{p.ParticipantName}: {p.ShareAmount:N2} {(p.IsSettled ? "(settled ✓)" : "(pending)")}");
            }
            var pending = parts.Where(p => !p.IsSettled).Sum(p => p.ShareAmount);
            sb.AppendLine("-----------");
            sb.AppendLine($"Still pending: {pending:N2}");
            sb.AppendLine("(via CampusCoin)");

            return Ok(new { success = true, text = sb.ToString() });
        }

        [HttpPost("splits/{splitId:int}/settle")]
        public async Task<IActionResult> MarkSettled(int splitId) => await SetSettled(splitId, true);

        [HttpPost("splits/{splitId:int}/unsettle")]
        public async Task<IActionResult> MarkUnsettled(int splitId) => await SetSettled(splitId, false);

        private async Task<IActionResult> SetSettled(int splitId, bool settled)
        {
            int userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var split = await _context.SharedExpenseSplits
                .Include(s => s.SharedExpense)
                .FirstOrDefaultAsync(s => s.SplitId == splitId);

            if (split?.SharedExpense == null)
                return NotFound(new { success = false, message = "Split not found." });
            if (split.SharedExpense.PayerUserId != userId)
                return Forbid();

            split.IsSettled = settled;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = settled
                    ? $"{split.ParticipantName} marked as settled ✓"
                    : $"{split.ParticipantName} marked as pending again"
            });
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            int userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var shared = await _context.SharedExpenses.FirstOrDefaultAsync(s => s.SharedExpenseId == id && s.PayerUserId == userId);
            if (shared == null) return NotFound(new { success = false, message = "Not found." });

            _context.SharedExpenseSplits.RemoveRange(_context.SharedExpenseSplits.Where(s => s.SharedExpenseId == id));
            _context.SharedExpenses.Remove(shared);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Split removed. Your expense entry in Transactions was kept." });
        }

        public class CreateSharedExpenseRequest
        {
            public string Description { get; set; } = "";
            public decimal TotalAmount { get; set; }
            public bool EqualSplit { get; set; } = true;
            /// <summary>If true, total ÷ (roommates + you). Only roommates are tracked as owing you.</summary>
            public bool IncludeMe { get; set; } = true;
            /// <summary>Optional: link an existing expense instead of creating a new one.</summary>
            public int? ExistingTransactionId { get; set; }
            public List<ParticipantDto> Participants { get; set; } = new();
        }

        public class UpdateSharedExpenseRequest
        {
            public string Description { get; set; } = "";
            public decimal TotalAmount { get; set; }
            public bool EqualSplit { get; set; } = true;
            public bool IncludeMe { get; set; } = true;
            public List<ParticipantDto> Participants { get; set; } = new();
        }

        public class ParticipantDto
        {
            public string Name { get; set; } = "";
            public decimal? ShareAmount { get; set; }
        }
    }
}
