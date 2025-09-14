using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CveWebApp.Data;
using CveWebApp.Models;

namespace CveWebApp.Controllers
{
    public class CveController : Controller
    {
        private readonly ApplicationDbContext _context;
        private const int PageSize = 20; // Set your preferred page size here

        public CveController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Cve
        public async Task<IActionResult> Index(
            int page = 1,
            string productFamily = null,
            string product = null,
            string maxSeverity = null,
            string article = null)
        {
            var query = _context.CveUpdateStagings.AsQueryable();

            if (!string.IsNullOrEmpty(productFamily))
                query = query.Where(c => c.ProductFamily == productFamily);

            if (!string.IsNullOrEmpty(product))
                query = query.Where(c => c.Product == product);

            if (!string.IsNullOrEmpty(maxSeverity))
                query = query.Where(c => c.MaxSeverity == maxSeverity);

            if (!string.IsNullOrEmpty(article))
                query = query.Where(c => c.Article.Contains(article));

            var totalItems = await query.CountAsync();

            var cveData = await query
                .OrderByDescending(c => c.ReleaseDate)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            // For filter dropdowns
            ViewBag.ProductFamilies = await _context.CveUpdateStagings
                .Select(c => c.ProductFamily).Distinct().OrderBy(x => x).ToListAsync();

            if (!string.IsNullOrEmpty(productFamily))
            {
                ViewBag.Products = await _context.CveUpdateStagings
                    .Where(c => c.ProductFamily == productFamily)
                    .Select(c => c.Product)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToListAsync();
            }
            else
            {
                ViewBag.Products = await _context.CveUpdateStagings
                    .Select(c => c.Product)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToListAsync();
            }

            ViewBag.MaxSeverities = await _context.CveUpdateStagings
                .Select(c => c.MaxSeverity).Distinct().OrderBy(x => x).ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)PageSize);
            ViewBag.SelectedProductFamily = productFamily;
            ViewBag.SelectedProduct = product;
            ViewBag.SelectedMaxSeverity = maxSeverity;
            ViewBag.ArticleQuery = article;

            return View(cveData);
        }

        // GET: Cve/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cveUpdateStaging = await _context.CveUpdateStagings
                .FirstOrDefaultAsync(m => m.Id == id);

            if (cveUpdateStaging == null)
            {
                return NotFound();
            }

            return View(cveUpdateStaging);
        }
    }
}