using CampusCoin.Data;
using CampusCoin.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CampusCoin.Controllers
{
    [Authorize(Roles = "Student")]
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public NotificationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out int id) ? id : 0;
        }

        // ================= GET: /api/notifications =================
        [HttpGet]
        public async Task<IActionResult> GetNotifications()
        {
            int userId = GetCurrentUserId();
            var list = await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(20)
                .Select(n => new NotificationDto
                {
                    NotificationId = n.NotificationId,
                    Message = n.Message,
                    Type = n.Type,
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt.ToString("MMM dd, yyyy h:mm tt")
                })
                .ToListAsync();

            int unreadCount = list.Count(n => !n.IsRead);
            return Ok(new { notifications = list, unreadCount });
        }

        // ================= PUT: /api/notifications/{id}/read =================
        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            int userId = GetCurrentUserId();
            var notif = await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == id && n.UserId == userId);

            if (notif == null) return NotFound();

            notif.IsRead = true;
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // ================= PUT: /api/notifications/read-all =================
        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllRead()
        {
            int userId = GetCurrentUserId();
            var unread = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            foreach (var n in unread)
            {
                n.IsRead = true;
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, markedCount = unread.Count });
        }
    }
}
