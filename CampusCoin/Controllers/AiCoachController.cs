





using CampusCoin.Data;
using CampusCoin.Models;
using CampusCoin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;

namespace CampusCoin.Controllers
{
    [Authorize(Roles = "Student")]
    public class AiCoachController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly GeminiService _gemini;

        public AiCoachController(ApplicationDbContext context, GeminiService gemini)
        {
            _context = context;
            _gemini = gemini;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out int id) ? id : 0;
        }

        [HttpGet("/AiCoach")]
        public IActionResult Index() => View();

        // ============================================================
        //  🔥 REAL AI ENDPOINT
        // ============================================================
        [HttpPost("/api/aicoach/ask")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> AskAI([FromBody] AskRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Message))
                    return BadRequest(new { success = false, message = "Message required" });

                int userId = GetCurrentUserId();
                if (userId == 0) return Unauthorized(new { success = false, message = "Not logged in" });

                // Save user message
                _context.ChatMessages.Add(new ChatMessage
                {
                    UserId = userId,
                    Sender = "user",
                    Message = request.Message,
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                // Build context
                var systemPrompt = await BuildSystemPromptAsync(userId);
                if (systemPrompt == null) return NotFound(new { success = false, message = "User not found" });

                // Call Gemini
                var aiReply = await _gemini.AskAsync(systemPrompt, request.Message);

                if (string.IsNullOrWhiteSpace(aiReply))
                {
                    // Gemini is unreachable (e.g. API key/auth issue) — don't leave the
                    // student with a dead-end message. Answer from their real data instead.
                    aiReply = await BuildLocalFallbackReplyAsync(userId, request.Message);
                }

                // Save AI reply
                _context.ChatMessages.Add(new ChatMessage
                {
                    UserId = userId,
                    Sender = "ai",
                    Message = aiReply,
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                return Ok(new { success = true, reply = aiReply });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ============================================================
        //  BUILD CONTEXT
        // ============================================================
        private async Task<string?> BuildSystemPromptAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return null;

            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1);

            var allTxns = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId)
                .ToListAsync();

            var monthTxns = allTxns.Where(t => t.Date >= monthStart).ToList();

            decimal totalIncome = allTxns.Where(t => t.Type == "Income").Sum(t => t.Amount);
            decimal totalExpense = allTxns.Where(t => t.Type == "Expense").Sum(t => t.Amount);
            decimal balance = totalIncome - totalExpense;

            decimal monthIncome = monthTxns.Where(t => t.Type == "Income").Sum(t => t.Amount);
            decimal monthExpense = monthTxns.Where(t => t.Type == "Expense").Sum(t => t.Amount);

            var catBreakdown = monthTxns
                .Where(t => t.Type == "Expense")
                .GroupBy(t => t.Category != null ? t.Category.Name : "Other")
                .Select(g => new { Category = g.Key, Total = g.Sum(x => x.Amount), Count = g.Count() })
                .OrderByDescending(x => x.Total)
                .ToList();

            var budgets = await _context.Budgets
                .Include(b => b.Category)
                .Where(b => b.UserId == userId)
                .ToListAsync();

            var goals = await _context.SavingsGoals
                .Where(g => g.UserId == userId)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("=== STUDENT'S REAL FINANCIAL DATA ===");
            sb.AppendLine($"Name: {user.FullName}");
            sb.AppendLine($"Academic Year: {user.AcademicYear ?? "N/A"}");
            sb.AppendLine($"Monthly Allowance Baseline: Rs. {user.MonthlyAllowanceBaseline:F0}");
            sb.AppendLine($"Monthly Savings Goal: Rs. {user.MonthlySavingsGoal:F0}");
            sb.AppendLine();
            sb.AppendLine("--- CURRENT BALANCE ---");
            sb.AppendLine($"Total Balance: Rs. {balance:F0}");
            sb.AppendLine($"All-time Income: Rs. {totalIncome:F0}");
            sb.AppendLine($"All-time Expense: Rs. {totalExpense:F0}");
            sb.AppendLine();
            sb.AppendLine($"--- THIS MONTH ({monthStart:MMM yyyy}) ---");
            sb.AppendLine($"Income: Rs. {monthIncome:F0}");
            sb.AppendLine($"Expense: Rs. {monthExpense:F0}");
            sb.AppendLine($"Net: Rs. {monthIncome - monthExpense:F0}");
            sb.AppendLine($"Transaction Count: {monthTxns.Count}");
            sb.AppendLine();
            sb.AppendLine("--- SPENDING BY CATEGORY (this month) ---");
            if (catBreakdown.Any())
            {
                foreach (var c in catBreakdown)
                    sb.AppendLine($"• {c.Category}: Rs. {c.Total:F0} ({c.Count} txns)");
            }
            else sb.AppendLine("No expenses tracked this month yet.");
            sb.AppendLine();
            sb.AppendLine("--- BUDGETS ---");
            if (budgets.Any())
            {
                foreach (var b in budgets)
                {
                    var spent = allTxns.Where(t => t.Type == "Expense" && t.CategoryId == b.CategoryId).Sum(t => t.Amount);
                    var pct = b.LimitAmount > 0 ? (spent / b.LimitAmount * 100) : 0;
                    sb.AppendLine($"• {b.Category?.Name}: Rs. {spent:F0} / {b.LimitAmount:F0} ({pct:F0}%)");
                }
            }
            else sb.AppendLine("No budgets set.");
            sb.AppendLine();
            sb.AppendLine("--- SAVINGS GOALS ---");
            if (goals.Any())
            {
                foreach (var g in goals)
                {
                    var pct = g.TargetAmount > 0 ? (g.CurrentAmount / g.TargetAmount * 100) : 0;
                    sb.AppendLine($"• {g.GoalName}: Rs. {g.CurrentAmount:F0} / {g.TargetAmount:F0} ({pct:F0}%)");
                }
            }
            else sb.AppendLine("No savings goals yet.");
            sb.AppendLine();
            sb.AppendLine("--- RECENT 10 TRANSACTIONS ---");
            foreach (var t in allTxns.OrderByDescending(t => t.Date).Take(10))
            {
                sb.AppendLine($"• {t.Date:yyyy-MM-dd} | {t.Type} | {t.Category?.Name ?? "Other"} | Rs. {t.Amount:F0}");
            }

            var prompt = $@"You are 'CampusCoin AI Coach' - a friendly financial advisor for college students in Pakistan.

RULES:
1. Answer ANY question about money, budget, saving, investing, or student finances.
2. Use the STUDENT'S REAL DATA below to give SPECIFIC, PERSONALIZED answers with exact Rs. numbers.
3. Respond in simple English. Keep answers 3-6 lines unless detailed analysis asked.
4. Use emojis sparingly (💰 📊 🎯 ✅ ⚠️ 💡).
5. Bold (**text**) key numbers.
6. If question is off-topic, say: ""I'm your finance coach - let's focus on money matters!""
7. NEVER invent data. If number isn't in records, say ""I don't see that in your records.""
8. Convert advice to Rs. amounts.
9. Tone: Encouraging, honest, supportive.

{sb}";

            return prompt;
        }

        // ============================================================
        //  LOCAL FALLBACK (used only when the Gemini API is unreachable —
        //  e.g. an invalid/expired key — so chat still gives a real,
        //  data-backed answer instead of a dead-end error every time)
        // ============================================================
        private async Task<string> BuildLocalFallbackReplyAsync(int userId, string message)
        {
            var user = await _context.Users.FindAsync(userId);
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1);

            var allTxns = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId)
                .ToListAsync();
            var monthTxns = allTxns.Where(t => t.Date >= monthStart).ToList();

            decimal totalIncome = allTxns.Where(t => t.Type == "Income").Sum(t => t.Amount);
            decimal totalExpense = allTxns.Where(t => t.Type == "Expense").Sum(t => t.Amount);
            decimal balance = totalIncome - totalExpense;
            decimal monthExpense = monthTxns.Where(t => t.Type == "Expense").Sum(t => t.Amount);

            var topCategory = monthTxns
                .Where(t => t.Type == "Expense")
                .GroupBy(t => t.Category != null ? t.Category.Name : "Other")
                .Select(g => new { Category = g.Key, Total = g.Sum(x => x.Amount) })
                .OrderByDescending(x => x.Total)
                .FirstOrDefault();

            var goals = await _context.SavingsGoals.Where(g => g.UserId == userId).ToListAsync();

            var m = message.ToLowerInvariant();
            string reply;

            if (m.Contains("balance"))
            {
                reply = $"💰 Your current balance is **Rs. {balance:F0}** (Income: Rs. {totalIncome:F0}, Expense: Rs. {totalExpense:F0}).";
            }
            else if (m.Contains("spend") || m.Contains("categor"))
            {
                reply = topCategory != null
                    ? $"📊 This month your top spending category is **{topCategory.Category}** at **Rs. {topCategory.Total:F0}**, out of Rs. {monthExpense:F0} total expenses so far."
                    : "📊 I don't see any expenses tracked for this month yet.";
            }
            else if (m.Contains("goal") || m.Contains("save") || m.Contains("saving"))
            {
                reply = goals.Any()
                    ? "🎯 Your savings goals: " + string.Join(", ", goals.Select(g => $"{g.GoalName} (Rs. {g.CurrentAmount:F0}/{g.TargetAmount:F0})"))
                    : "🎯 You haven't set any savings goals yet — head to the Saving Goals page to create one.";
            }
            else
            {
                reply = $"💡 Based on your records: balance is **Rs. {balance:F0}** and you've spent **Rs. {monthExpense:F0}** this month so far.";
            }

            reply += "\n\n⚠️ *Note: I answered from your CampusCoin records. For richer AI replies, make sure a valid Gemini API key is configured.*";
            return reply;
        }

        // ============================================================
        //  CHAT HISTORY
        // ============================================================
        [HttpGet("/api/aicoach/chat")]
        public async Task<IActionResult> GetChatHistory()
        {
            int userId = GetCurrentUserId();
            var msgs = await _context.ChatMessages
                .Where(c => c.UserId == userId)
                .OrderBy(c => c.CreatedAt)
                .Take(100)
                .Select(c => new { c.Id, c.Sender, c.Message, c.CreatedAt })
                .ToListAsync();
            return Ok(msgs);
        }

        [HttpDelete("/api/aicoach/chat")]
        public async Task<IActionResult> ClearChat()
        {
            int userId = GetCurrentUserId();
            var msgs = await _context.ChatMessages.Where(c => c.UserId == userId).ToListAsync();
            _context.ChatMessages.RemoveRange(msgs);
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // ============================================================
        //  TICKETS
        // ============================================================
        [HttpGet("/api/aicoach/tickets")]
        public async Task<IActionResult> GetTickets([FromQuery] string? status = null)
        {
            int userId = GetCurrentUserId();
            var q = _context.SupportTickets.Where(t => t.UserId == userId);
            if (!string.IsNullOrWhiteSpace(status) && status != "all")
                q = q.Where(t => t.Status == status);
            return Ok(await q.OrderByDescending(t => t.CreatedAt).ToListAsync());
        }

        [HttpPost("/api/aicoach/tickets")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> CreateTicket([FromBody] TicketRequest model)
        {
            int userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(model.Subject))
                return BadRequest(new { success = false, message = "Subject required." });

            var ticket = new SupportTicket
            {
                UserId = userId,
                TicketCode = "TCK-" + new Random().Next(1000, 9999),
                Subject = model.Subject.Trim(),
                Category = model.Category ?? "General",
                Priority = model.Priority ?? "Medium",
                Message = model.Message ?? "",
                Status = "open",
                CreatedAt = DateTime.UtcNow
            };
            _context.SupportTickets.Add(ticket);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, id = ticket.Id, code = ticket.TicketCode });
        }

        [HttpPut("/api/aicoach/tickets/{id}/status")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> UpdateTicketStatus(int id, [FromBody] TicketStatusRequest model)
        {
            int userId = GetCurrentUserId();
            var t = await _context.SupportTickets.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
            if (t == null) return NotFound();
            t.Status = model.Status;
            t.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [HttpDelete("/api/aicoach/tickets/{id}")]
        public async Task<IActionResult> DeleteTicket(int id)
        {
            int userId = GetCurrentUserId();
            var t = await _context.SupportTickets.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
            if (t == null) return NotFound();
            _context.SupportTickets.Remove(t);
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [HttpGet("/api/aicoach/stats")]
        public async Task<IActionResult> GetStats()
        {
            int userId = GetCurrentUserId();
            int activeTickets = await _context.SupportTickets.CountAsync(t => t.UserId == userId && t.Status != "resolved");
            int totalChats = await _context.ChatMessages.CountAsync(c => c.UserId == userId);
            return Ok(new { activeTickets, totalChats });
        }
    }

    // DTOs
    public class AskRequest
    {
        public string Message { get; set; } = string.Empty;
    }

    public class TicketRequest
    {
        public string Subject { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? Priority { get; set; }
        public string? Message { get; set; }
    }

    public class TicketStatusRequest
    {
        public string Status { get; set; } = "open";
    }
}