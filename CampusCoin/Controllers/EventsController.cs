using CampusCoin.Data;
using CampusCoin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CampusCoin.Controllers
{
    [Authorize(Roles = "Student")]
    public class EventsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EventsController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out int id) ? id : 0;
        }

        [HttpGet("/Events")]
        public IActionResult Index() => View();

        [HttpGet("/api/events")]
        public async Task<IActionResult> GetEvents()
        {
            int userId = GetCurrentUserId();
            var events = await _context.Events.OrderByDescending(e => e.EventDate).ToListAsync();
            var myRegs = await _context.EventRegistrations.Where(r => r.UserId == userId).ToListAsync();

            var result = events.Select(e => new
            {
                id = e.Id,
                eventCode = e.EventCode,
                title = e.Title,
                category = e.Category,
                status = e.Status,
                image = e.Image,
                date = e.EventDate.ToString("MMM dd, yyyy"),
                time = e.Time,
                venue = e.Venue,
                fee = e.Fee,
                seatsLeft = e.SeatsLeft,
                description = e.Description,
                registered = myRegs.Any(r => r.EventId == e.Id),
                passCode = myRegs.FirstOrDefault(r => r.EventId == e.Id)?.PassCode
            });

            return Ok(result);
        }

        [HttpPost("/api/events/{id}/register")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Register(int id)
        {
            try
            {
                int userId = GetCurrentUserId();
                var evt = await _context.Events.FindAsync(id);
                if (evt == null) return NotFound(new { success = false, message = "Event not found" });

                if (await _context.EventRegistrations.AnyAsync(r => r.UserId == userId && r.EventId == id))
                    return BadRequest(new { success = false, message = "Already registered" });

                if (evt.SeatsLeft <= 0)
                    return BadRequest(new { success = false, message = "Event is full" });

                var user = await _context.Users.FindAsync(userId);
                if (user != null && evt.Fee > 0)
                {
                    if (user.MonthlyAllowanceBaseline < evt.Fee)
                        return BadRequest(new { success = false, message = "Insufficient wallet balance" });

                    user.MonthlyAllowanceBaseline -= evt.Fee;

                    var cat = await _context.Categories.FirstOrDefaultAsync(c =>
                        c.Type == "Expense" && (c.UserId == null || c.UserId == userId) &&
                        (c.Name == "Entertainment" || c.Name == "Miscellaneous" || c.Name == "Food"));
                    if (cat == null)
                    {
                        cat = new Category { Name = "Entertainment", Type = "Expense", IsDefault = false, UserId = userId };
                        _context.Categories.Add(cat);
                        await _context.SaveChangesAsync();
                    }

                    _context.Transactions.Add(new Transaction
                    {
                        UserId = userId,
                        CategoryId = cat.CategoryId,
                        Amount = evt.Fee,
                        Type = "Expense",
                        Description = $"Event: {evt.Title}",
                        Date = DateTime.UtcNow.Date,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                evt.SeatsLeft--;

                var passCode = $"PASS-{new Random().Next(1000, 9999)}-{evt.Id}";
                _context.EventRegistrations.Add(new EventRegistration
                {
                    EventId = id,
                    UserId = userId,
                    PassCode = passCode,
                    RegisteredAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Registered successfully", passCode });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
}