using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using CveWebApp.Data;
using CveWebApp.Models;

namespace CveWebApp.Controllers
{
    /// <summary>
    /// Controller for viewing application access logs - restricted to Admin role only
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class AccessLogsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccessLogsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: AccessLogs
        public async Task<IActionResult> Index(AccessLogsViewModel model)
        {
            var query = _context.LoginAttempts.AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(model.UsernameFilter))
            {
                query = query.Where(l => l.Username.Contains(model.UsernameFilter) || 
                                        (l.Email != null && l.Email.Contains(model.UsernameFilter)));
            }

            if (!string.IsNullOrEmpty(model.IpFilter))
            {
                query = query.Where(l => l.SourceIP.Contains(model.IpFilter));
            }

            if (model.SuccessFilter.HasValue)
            {
                query = query.Where(l => l.IsSuccess == model.SuccessFilter.Value);
            }

            if (model.FromDate.HasValue)
            {
                query = query.Where(l => l.Timestamp.Date >= model.FromDate.Value.Date);
            }

            if (model.ToDate.HasValue)
            {
                query = query.Where(l => l.Timestamp.Date <= model.ToDate.Value.Date);
            }

            // Get total count for pagination
            model.TotalItems = await query.CountAsync();

            // Apply pagination and ordering
            model.LoginAttempts = await query
                .OrderByDescending(l => l.Timestamp)
                .Skip((model.Page - 1) * model.PageSize)
                .Take(model.PageSize)
                .ToListAsync();

            return View(model);
        }

        // GET: AccessLogs/Details/{id}
        public async Task<IActionResult> Details(int id)
        {
            var loginAttempt = await _context.LoginAttempts.FindAsync(id);
            if (loginAttempt == null)
            {
                return NotFound();
            }

            return PartialView("_LogDetailsModal", loginAttempt);
        }

        // POST: AccessLogs/Clear - Clears old log entries
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Clear(int daysToKeep = 30)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);
            var oldLogs = _context.LoginAttempts.Where(l => l.Timestamp < cutoffDate);
            
            var count = await oldLogs.CountAsync();
            _context.LoginAttempts.RemoveRange(oldLogs);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Cleared {count} log entries older than {daysToKeep} days.";
            return RedirectToAction(nameof(Index));
        }
    }
}