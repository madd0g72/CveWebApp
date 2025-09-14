using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using CVEWebApp.Data; // Adjust to your actual namespace
using CVEWebApp.Models; // Adjust to your actual namespace

namespace CVEWebApp.Controllers
{
    public class CveController : Controller
    {
        private readonly ApplicationDbContext _context;
        private const int PageSize = 50;

        public CveController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int page = 1)
        {
            var totalItems = await _context.CveUpdateStagings.CountAsync();

            var cveData = await _context.CveUpdateStagings
                .OrderByDescending(c => c.ReleaseDate)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)PageSize);

            return View(cveData);
        }
    }
}