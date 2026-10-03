using CampusCoin.Data;
using CampusCoin.Services;
using CampusCoin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;

namespace CampusCoin.Controllers
{
    [Authorize(Roles = "Student")]
    [ApiController]
    [Route("api/[controller]")]
    public class ReportsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService? _email;

        public ReportsController(ApplicationDbContext context, IEmailService? email = null)
        {
            _context = context;
            _email = email;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out int id) ? id : 0;
        }

        // ================= GET: /api/reports/monthly =================
        [HttpGet("monthly")]
        public async Task<IActionResult> GetMonthlyReport(
            [FromQuery] string? month = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string? category = null)
        {
            int userId = GetCurrentUserId();

            DateTime start;
            DateTime end;

            if (startDate.HasValue && endDate.HasValue)
            {
                start = startDate.Value.Date;
                end = endDate.Value.Date.AddDays(1);
            }
            else if (!string.IsNullOrWhiteSpace(month) && DateTime.TryParse(month + "-01", out var parsed))
            {
                start = new DateTime(parsed.Year, parsed.Month, 1);
                end = start.AddMonths(1);
            }
            else
            {
                var now = DateTime.UtcNow;
                start = new DateTime(now.Year, now.Month, 1);
                end = start.AddMonths(1);
            }

            var query = _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId && t.Date >= start && t.Date < end);

            if (!string.IsNullOrWhiteSpace(category) && category != "ALL")
            {
                query = query.Where(t => t.Category != null && t.Category.Name.ToLower() == category.Trim().ToLower());
            }

            var txns = await query.OrderByDescending(t => t.Date).ToListAsync();

            decimal totalIncome = txns.Where(t => t.Type == "Income").Sum(t => t.Amount);
            decimal totalExpense = txns.Where(t => t.Type == "Expense").Sum(t => t.Amount);
            decimal netSavings = totalIncome - totalExpense;
            decimal savingsRate = totalIncome > 0 ? Math.Round((netSavings / totalIncome) * 100m, 1) : 0m;

            // Category breakdown
            var categoryBreakdown = txns
                .GroupBy(t => new { Name = t.Category?.Name ?? "Uncategorized", Type = t.Type })
                .Select(g => new
                {
                    Category = g.Key.Name,
                    Type = g.Key.Type,
                    Total = g.Sum(x => x.Amount),
                    Count = g.Count(),
                    Percentage = g.Key.Type == "Expense" && totalExpense > 0
                        ? Math.Round((g.Sum(x => x.Amount) / totalExpense) * 100m, 1)
                        : (g.Key.Type == "Income" && totalIncome > 0 ? Math.Round((g.Sum(x => x.Amount) / totalIncome) * 100m, 1) : 0m)
                })
                .OrderByDescending(x => x.Total)
                .ToList();

            // Daily breakdown
            var dailyBreakdown = txns
                .Where(t => t.Type == "Expense")
                .GroupBy(t => t.Date.ToString("yyyy-MM-dd"))
                .Select(g => new
                {
                    Date = g.Key,
                    Total = g.Sum(x => x.Amount),
                    Count = g.Count()
                })
                .OrderBy(x => x.Date)
                .ToList();

            // Weekly spending summary
            var weeklyBreakdown = txns
                .Where(t => t.Type == "Expense")
                .GroupBy(t =>
                {
                    // ISO-like week key: year-Wxx starting Monday
                    var d = t.Date.Date;
                    var diff = (7 + (d.DayOfWeek - DayOfWeek.Monday)) % 7;
                    var monday = d.AddDays(-diff);
                    return monday.ToString("yyyy-MM-dd");
                })
                .Select(g => new
                {
                    WeekStarting = g.Key,
                    Total = g.Sum(x => x.Amount),
                    Count = g.Count(),
                    Days = g.Select(x => x.Date.ToString("yyyy-MM-dd")).Distinct().Count()
                })
                .OrderBy(x => x.WeekStarting)
                .ToList();

            return Ok(new
            {
                startDate = start.ToString("yyyy-MM-dd"),
                endDate = end.AddDays(-1).ToString("yyyy-MM-dd"),
                totalIncome,
                totalExpense,
                netSavings,
                savingsRate,
                transactionCount = txns.Count,
                categoryBreakdown,
                dailyBreakdown,
                weeklyBreakdown
            });
        }

        // ================= GET: /api/reports/printable =================
        // Server-rendered HTML report optimized for browser "Save as PDF" / print.
        [HttpGet("printable")]
        public async Task<IActionResult> GetPrintableReport(
            [FromQuery] string? month = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            int userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var user = await _context.Users.FindAsync(userId);
            DateTime start, end;
            if (startDate.HasValue && endDate.HasValue)
            {
                start = startDate.Value.Date;
                end = endDate.Value.Date.AddDays(1);
            }
            else if (!string.IsNullOrWhiteSpace(month) && DateTime.TryParse(month + "-01", out var parsed))
            {
                start = new DateTime(parsed.Year, parsed.Month, 1);
                end = start.AddMonths(1);
            }
            else
            {
                var now = DateTime.UtcNow;
                start = new DateTime(now.Year, now.Month, 1);
                end = start.AddMonths(1);
            }

            var txns = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId && t.Date >= start && t.Date < end)
                .OrderByDescending(t => t.Date)
                .ToListAsync();

            decimal totalIncome = txns.Where(t => t.Type == "Income").Sum(t => t.Amount);
            decimal totalExpense = txns.Where(t => t.Type == "Expense").Sum(t => t.Amount);
            decimal net = totalIncome - totalExpense;
            decimal savingsRate = totalIncome > 0 ? Math.Round((net / totalIncome) * 100m, 1) : 0m;

            var cats = txns.Where(t => t.Type == "Expense")
                .GroupBy(t => t.Category?.Name ?? "Other")
                .Select(g => new { Name = g.Key, Total = g.Sum(x => x.Amount), Count = g.Count() })
                .OrderByDescending(x => x.Total)
                .ToList();

            // Weekly for printable
            var weekly = txns.Where(t => t.Type == "Expense")
                .GroupBy(t =>
                {
                    var d = t.Date.Date;
                    var diff = (7 + (d.DayOfWeek - DayOfWeek.Monday)) % 7;
                    return d.AddDays(-diff).ToString("yyyy-MM-dd");
                })
                .Select(g => new { Week = g.Key, Total = g.Sum(x => x.Amount), Count = g.Count() })
                .OrderBy(x => x.Week)
                .ToList();

            string H(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");
            string Money(decimal v) => v.ToString("N2");

            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html lang='en'><head><meta charset='utf-8'>");
            sb.Append("<meta name='viewport' content='width=device-width,initial-scale=1'>");
            sb.Append("<title>CampusCoin Monthly Report</title>");
            sb.Append("<style>");
            sb.Append(@"
:root {
  --purple: #7c3aed;
  --purple-dark: #5b21b6;
  --purple-soft: #f5f3ff;
  --ink: #0f172a;
  --muted: #64748b;
  --line: #e2e8f0;
  --green: #059669;
  --rose: #e11d48;
  --card: #ffffff;
  --bg: #f8fafc;
}
* { box-sizing: border-box; }
body {
  margin: 0;
  font-family: 'Segoe UI', system-ui, -apple-system, sans-serif;
  color: var(--ink);
  background: var(--bg);
  line-height: 1.45;
}
.toolbar {
  position: sticky; top: 0; z-index: 20;
  display: flex; align-items: center; justify-content: space-between; gap: 12px;
  padding: 12px 24px;
  background: rgba(255,255,255,.92);
  backdrop-filter: blur(10px);
  border-bottom: 1px solid var(--line);
}
.toolbar .hint { font-size: 12px; color: var(--muted); }
.btn {
  appearance: none; border: none; cursor: pointer;
  background: linear-gradient(135deg, var(--purple), var(--purple-dark));
  color: #fff; font-weight: 700; font-size: 13px;
  padding: 10px 18px; border-radius: 12px;
  box-shadow: 0 8px 20px rgba(124,58,237,.25);
}
.btn:hover { filter: brightness(1.05); }
.btn-ghost {
  background: #fff; color: var(--purple-dark);
  border: 1px solid var(--line); box-shadow: none;
}
.page {
  max-width: 920px;
  margin: 24px auto 48px;
  padding: 0 20px;
}
.sheet {
  background: var(--card);
  border: 1px solid var(--line);
  border-radius: 24px;
  overflow: hidden;
  box-shadow: 0 20px 50px rgba(15,23,42,.06);
}
.hero {
  background: linear-gradient(135deg, #6d28d9 0%, #7c3aed 45%, #4f46e5 100%);
  color: #fff;
  padding: 28px 32px 24px;
  position: relative;
}
.hero::after {
  content: '';
  position: absolute; right: -40px; top: -40px;
  width: 180px; height: 180px; border-radius: 50%;
  background: rgba(255,255,255,.08);
}
.brand {
  display: flex; align-items: center; gap: 12px; margin-bottom: 18px;
}
.logo {
  width: 42px; height: 42px; border-radius: 14px;
  background: rgba(255,255,255,.18);
  display: flex; align-items: center; justify-content: center;
  font-weight: 900; font-size: 18px; letter-spacing: -1px;
}
.brand-text { font-size: 13px; opacity: .85; font-weight: 600; letter-spacing: .04em; text-transform: uppercase; }
.hero h1 { margin: 0; font-size: 28px; letter-spacing: -0.6px; font-weight: 800; }
.hero .meta { margin-top: 8px; font-size: 13px; opacity: .9; }
.body { padding: 28px 32px 32px; }
.kpis {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 14px;
  margin-bottom: 28px;
}
.kpi {
  border: 1px solid var(--line);
  border-radius: 16px;
  padding: 16px;
  background: linear-gradient(180deg, #fff, #fafafa);
}
.kpi .label { font-size: 11px; font-weight: 700; color: var(--muted); text-transform: uppercase; letter-spacing: .06em; }
.kpi .value { margin-top: 6px; font-size: 22px; font-weight: 800; letter-spacing: -0.5px; }
.kpi.income .value { color: var(--green); }
.kpi.expense .value { color: var(--rose); }
.kpi.net .value { color: var(--purple); }
.section-title {
  display: flex; align-items: center; justify-content: space-between;
  margin: 8px 0 12px;
}
.section-title h2 {
  margin: 0; font-size: 15px; font-weight: 800; letter-spacing: -0.2px;
}
.section-title span { font-size: 12px; color: var(--muted); }
table {
  width: 100%; border-collapse: collapse; font-size: 12.5px;
  margin-bottom: 24px;
}
thead th {
  text-align: left; font-size: 11px; text-transform: uppercase;
  letter-spacing: .05em; color: var(--muted); font-weight: 700;
  background: var(--purple-soft);
  padding: 10px 12px;
  border-bottom: 1px solid #ddd6fe;
}
tbody td {
  padding: 10px 12px;
  border-bottom: 1px solid var(--line);
  vertical-align: top;
}
tbody tr:last-child td { border-bottom: none; }
tbody tr:hover { background: #faf5ff; }
.right { text-align: right; }
.badge {
  display: inline-block; padding: 3px 8px; border-radius: 999px;
  font-size: 10px; font-weight: 700;
}
.badge.income { background: #d1fae5; color: #065f46; }
.badge.expense { background: #ffe4e6; color: #9f1239; }
.bar-wrap { display: flex; align-items: center; gap: 10px; }
.bar {
  height: 8px; border-radius: 999px; background: #ede9fe; flex: 1; overflow: hidden; min-width: 60px;
}
.bar > i {
  display: block; height: 100%;
  background: linear-gradient(90deg, #8b5cf6, #6d28d9);
  border-radius: 999px;
}
.weeks {
  display: flex; flex-wrap: wrap; gap: 8px; margin-bottom: 24px;
}
.week-chip {
  border: 1px solid var(--line); border-radius: 12px;
  padding: 10px 12px; background: #fff; min-width: 140px;
}
.week-chip .w { font-size: 11px; color: var(--muted); font-weight: 600; }
.week-chip .a { font-size: 14px; font-weight: 800; margin-top: 2px; color: var(--purple-dark); }
.footer {
  margin-top: 8px; padding-top: 16px; border-top: 1px dashed var(--line);
  font-size: 11px; color: var(--muted); display: flex; justify-content: space-between; gap: 12px; flex-wrap: wrap;
}
.empty {
  text-align: center; padding: 28px 12px; color: var(--muted); font-size: 13px;
  border: 1px dashed var(--line); border-radius: 14px; margin-bottom: 20px;
}
@media print {
  body { background: #fff; }
  .toolbar { display: none !important; }
  .page { margin: 0; max-width: none; padding: 0; }
  .sheet { border: none; box-shadow: none; border-radius: 0; }
  .hero { -webkit-print-color-adjust: exact; print-color-adjust: exact; }
  thead th, .kpi, .week-chip, .bar > i { -webkit-print-color-adjust: exact; print-color-adjust: exact; }
  tbody tr:hover { background: transparent; }
}
@media (max-width: 720px) {
  .kpis { grid-template-columns: 1fr 1fr; }
  .body, .hero { padding: 20px; }
}
");
            sb.Append("</style></head><body>");

            // Toolbar (hidden on print)
            sb.Append("<div class='toolbar no-print'>");
            sb.Append("<div class='hint'>Preview · Use <b>Print / Save as PDF</b> for a permanent copy</div>");
            sb.Append("<div style='display:flex;gap:8px'>");
            sb.Append("<button class='btn btn-ghost' onclick='window.close()'>Close</button>");
            sb.Append("<button class='btn' onclick='window.print()'>Print / Save as PDF</button>");
            sb.Append("</div></div>");

            sb.Append("<div class='page'><div class='sheet'>");

            // Hero
            sb.Append("<div class='hero'>");
            sb.Append("<div class='brand'><div class='logo'>CC</div><div class='brand-text'>CampusCoin · NextGen BudgetBee</div></div>");
            sb.Append("<h1>Monthly Financial Report</h1>");
            sb.Append($"<div class='meta'>{H(user?.FullName ?? "Student")} &nbsp;·&nbsp; {start:dd MMM yyyy} – {end.AddDays(-1):dd MMM yyyy} &nbsp;·&nbsp; Generated {DateTime.UtcNow:dd MMM yyyy HH:mm} UTC</div>");
            sb.Append("</div>");

            sb.Append("<div class='body'>");

            // KPIs
            sb.Append("<div class='kpis'>");
            sb.Append($"<div class='kpi income'><div class='label'>Income</div><div class='value'>{Money(totalIncome)}</div></div>");
            sb.Append($"<div class='kpi expense'><div class='label'>Expense</div><div class='value'>{Money(totalExpense)}</div></div>");
            sb.Append($"<div class='kpi net'><div class='label'>Net</div><div class='value'>{Money(net)}</div></div>");
            sb.Append($"<div class='kpi'><div class='label'>Transactions</div><div class='value'>{txns.Count}</div><div style='font-size:11px;color:var(--muted);margin-top:4px'>Savings rate {savingsRate}%</div></div>");
            sb.Append("</div>");

            // Weekly
            if (weekly.Count > 0)
            {
                sb.Append("<div class='section-title'><h2>Weekly spending</h2><span>Expenses by week</span></div>");
                sb.Append("<div class='weeks'>");
                foreach (var w in weekly)
                {
                    sb.Append($"<div class='week-chip'><div class='w'>Week of {w.Week}</div><div class='a'>{Money(w.Total)}</div><div class='w'>{w.Count} txn{(w.Count == 1 ? "" : "s")}</div></div>");
                }
                sb.Append("</div>");
            }

            // Category breakdown
            sb.Append("<div class='section-title'><h2>Category breakdown</h2><span>Expenses only</span></div>");
            if (cats.Count == 0)
            {
                sb.Append("<div class='empty'>No expense transactions in this range.</div>");
            }
            else
            {
                decimal maxCat = cats.Max(c => c.Total);
                if (maxCat <= 0) maxCat = 1;
                sb.Append("<table><thead><tr><th>Category</th><th>Share</th><th class='right'>Amount</th><th class='right'>Count</th></tr></thead><tbody>");
                foreach (var c in cats)
                {
                    var pct = totalExpense > 0 ? Math.Round((c.Total / totalExpense) * 100m, 1) : 0m;
                    var width = Math.Round((c.Total / maxCat) * 100m, 0);
                    sb.Append("<tr>");
                    sb.Append($"<td><b>{H(c.Name)}</b></td>");
                    sb.Append($"<td><div class='bar-wrap'><div class='bar'><i style='width:{width}%'></i></div><span style='min-width:42px;color:var(--muted)'>{pct}%</span></div></td>");
                    sb.Append($"<td class='right'><b>{Money(c.Total)}</b></td>");
                    sb.Append($"<td class='right'>{c.Count}</td>");
                    sb.Append("</tr>");
                }
                sb.Append("</tbody></table>");
            }

            // Transactions
            sb.Append("<div class='section-title'><h2>Transactions</h2><span>Up to 200 most recent</span></div>");
            if (txns.Count == 0)
            {
                sb.Append("<div class='empty'>No transactions found for this date range. Try a wider range on the Reports page.</div>");
            }
            else
            {
                sb.Append("<table><thead><tr><th>Date</th><th>Type</th><th>Category</th><th>Description</th><th class='right'>Amount</th></tr></thead><tbody>");
                foreach (var t in txns.Take(200))
                {
                    var badge = t.Type == "Income" ? "income" : "expense";
                    sb.Append("<tr>");
                    sb.Append($"<td>{t.Date:dd MMM yyyy}</td>");
                    sb.Append($"<td><span class='badge {badge}'>{H(t.Type)}</span></td>");
                    sb.Append($"<td>{H(t.Category?.Name ?? "—")}</td>");
                    sb.Append($"<td>{H(t.Description ?? "")}</td>");
                    sb.Append($"<td class='right'><b>{Money(t.Amount)}</b></td>");
                    sb.Append("</tr>");
                }
                sb.Append("</tbody></table>");
            }

            sb.Append("<div class='footer'>");
            sb.Append("<div>CampusCoin · Advisory only — not certified financial advice.</div>");
            sb.Append("<div>Print this page → Save as PDF for your records.</div>");
            sb.Append("</div>");

            sb.Append("</div></div></div>"); // body sheet page
            sb.Append("</body></html>");

            return Content(sb.ToString(), "text/html; charset=utf-8");
        }

        // ================= GET: /api/reports/export-csv =================
        // Used by the Student Profile page's "Export Student Data" button.
        [HttpGet("export-csv")]
        public async Task<IActionResult> ExportCsv()
        {
            int userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            var txns = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.Date)
                .ToListAsync();

            var budgets = await _context.Budgets
                .Include(b => b.Category)
                .Where(b => b.UserId == userId)
                .ToListAsync();

            var goals = await _context.SavingsGoals
                .Where(g => g.UserId == userId)
                .ToListAsync();

            string Csv(string? s)
            {
                if (string.IsNullOrEmpty(s)) return "";
                if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
                    return "\"" + s.Replace("\"", "\"\"") + "\"";
                return s;
            }

            var sb = new StringBuilder();
            sb.AppendLine("CampusCoin Data Export");
            sb.AppendLine($"Student,{Csv(user.FullName)}");
            sb.AppendLine($"Email,{Csv(user.Email)}");
            sb.AppendLine($"Exported At,{DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
            sb.AppendLine();

            sb.AppendLine("=== TRANSACTIONS ===");
            sb.AppendLine("Date,Type,Category,Amount,Description");
            foreach (var t in txns)
            {
                sb.AppendLine($"{t.Date:yyyy-MM-dd},{Csv(t.Type)},{Csv(t.Category?.Name ?? "Other")},{t.Amount},{Csv(t.Description)}");
            }
            sb.AppendLine();

            sb.AppendLine("=== BUDGETS ===");
            sb.AppendLine("Category,Limit Amount");
            foreach (var b in budgets)
            {
                sb.AppendLine($"{Csv(b.Category?.Name ?? "Uncategorized")},{b.LimitAmount}");
            }
            sb.AppendLine();

            sb.AppendLine("=== SAVINGS GOALS ===");
            sb.AppendLine("Goal Name,Target Amount,Current Amount");
            foreach (var g in goals)
            {
                sb.AppendLine($"{Csv(g.GoalName)},{g.TargetAmount},{g.CurrentAmount}");
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = $"campuscoin-export-{DateTime.UtcNow:yyyy-MM-dd}.csv";
            return File(bytes, "text/csv", fileName);
        }

        // ================= POST: /api/reports/export =================
        [HttpPost("export")]
        public async Task<IActionResult> LogExport([FromBody] ReportExportDto model)
        {
            int userId = GetCurrentUserId();

            var exportLog = new ReportExport
            {
                UserId = userId,
                ReportType = model.ReportType ?? "CategoryWise",
                RangeStart = model.StartDate,
                RangeEnd = model.EndDate,
                Format = model.Format ?? "PDF",
                ExportedAt = DateTime.UtcNow
            };

            _context.ReportExports.Add(exportLog);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, exportId = exportLog.ExportId });
        }

        // ================= POST: /api/reports/share-email =================
        [HttpPost("share-email")]
        public async Task<IActionResult> ShareReportEmail([FromBody] ShareReportRequest? model)
        {
            int userId = GetCurrentUserId();
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return Unauthorized();

            var to = (model?.Email ?? user.Email ?? "").Trim();
            if (string.IsNullOrWhiteSpace(to) || !to.Contains("@"))
                return BadRequest(new { success = false, message = "Valid email is required." });

            // Build a simple summary
            var start = DateTime.UtcNow.Date.AddDays(-(model?.Days ?? 30));
            var txns = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId && t.Date >= start)
                .ToListAsync();
            var income = txns.Where(t => t.Type == "Income").Sum(t => t.Amount);
            var expense = txns.Where(t => t.Type == "Expense").Sum(t => t.Amount);
            var top = txns.Where(t => t.Type == "Expense")
                .GroupBy(t => t.Category != null ? t.Category.Name : "Other")
                .OrderByDescending(g => g.Sum(x => x.Amount))
                .Take(5)
                .Select(g => $"• {g.Key}: {g.Sum(x => x.Amount):N0}")
                .ToList();

            var subject = model?.Subject ?? "Your CampusCoin spending summary";
            var body = $@"Hi {user.FullName ?? "there"},

Here is your CampusCoin summary (last {model?.Days ?? 30} days):

Income:  {income:N2}
Expense: {expense:N2}
Net:     {(income - expense):N2}

Top expense categories:
{string.Join("\n", top)}

This is an automated summary from CampusCoin — not financial advice.
";
            try
            {
                if (_email == null)
                    return StatusCode(503, new { success = false, message = "Email service unavailable." });
                var result = await _email.SendAsync(to, subject, body.Replace("\n", "<br/>"));
                if (!result.Success)
                    return StatusCode(500, new { success = false, message = result.Error ?? "Email failed." });
                return Ok(new { success = true, message = "Summary sent to " + to });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Email failed: " + ex.Message });
            }
        }

        public class ShareReportRequest
        {
            public string? Email { get; set; }
            public string? Subject { get; set; }
            public int Days { get; set; } = 30;
        }

    }

    public class ReportExportDto
    {
        public string? ReportType { get; set; } = "CategoryWise";
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Format { get; set; } = "PDF";
    }
}
