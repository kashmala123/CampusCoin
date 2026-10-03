using CampusCoin.Data;
using CampusCoin.Models;
using CampusCoin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CampusCoin.Controllers
{
    [Authorize(Roles = "Student,Admin")]
    [ApiController]
    [Route("api/[controller]")]
    public class TipsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly SmartTipsEngine _tipsEngine;

        public TipsController(ApplicationDbContext context, SmartTipsEngine tipsEngine)
        {
            _context = context;
            _tipsEngine = tipsEngine;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out int id) ? id : 0;
        }

        // ================= GET: /api/tips =================
        [HttpGet]
        public async Task<IActionResult> GetTips()
        {
            int userId = GetCurrentUserId();
            var tips = await _tipsEngine.GenerateTipsAsync(userId, _context, DateTime.UtcNow);
            return Ok(tips);
        }

        // ================= POST: /api/tips/{id}/dismiss =================
        [HttpPost("{id}/dismiss")]
        public async Task<IActionResult> DismissTip(int id)
        {
            int userId = GetCurrentUserId();
            var interaction = await _context.TipInteractions
                .FirstOrDefaultAsync(i => i.UserId == userId && i.TipId == id);

            if (interaction == null)
            {
                interaction = new TipInteraction
                {
                    UserId = userId,
                    TipId = id,
                    IsDismissed = true,
                    IsPinned = false,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.TipInteractions.Add(interaction);
            }
            else
            {
                interaction.IsDismissed = true;
                interaction.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Tip dismissed." });
        }

        // ================= POST: /api/tips/{id}/pin =================
        [HttpPost("{id}/pin")]
        public async Task<IActionResult> PinTip(int id)
        {
            int userId = GetCurrentUserId();
            var interaction = await _context.TipInteractions
                .FirstOrDefaultAsync(i => i.UserId == userId && i.TipId == id);

            if (interaction == null)
            {
                interaction = new TipInteraction
                {
                    UserId = userId,
                    TipId = id,
                    IsDismissed = false,
                    IsPinned = true,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.TipInteractions.Add(interaction);
            }
            else
            {
                interaction.IsPinned = !interaction.IsPinned;
                interaction.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, isPinned = interaction.IsPinned });
        }
    }
}
