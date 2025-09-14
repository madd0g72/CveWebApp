using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CveWebApp.Data;
using CveWebApp.Models;

namespace CveWebApp.Controllers
{
    public class CveController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CveController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Cve
        public async Task<IActionResult> Index()
        {
            var cveData = await _context.CveUpdateStagings
                .OrderByDescending(c => c.ReleaseDate)
                .ToListAsync();
            
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

        // GET: Cve/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Cve/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,ReleaseDate,ProductFamily,Product,Platform,Impact,MaxSeverity,Article,ArticleLink,Supercedence,Download,DownloadLink,BuildNumber,Details,DetailsLink,BaseScore,TemporalScore,CustomerActionRequired")] CveUpdateStaging cveUpdateStaging)
        {
            if (ModelState.IsValid)
            {
                _context.Add(cveUpdateStaging);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(cveUpdateStaging);
        }

        // GET: Cve/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cveUpdateStaging = await _context.CveUpdateStagings.FindAsync(id);
            if (cveUpdateStaging == null)
            {
                return NotFound();
            }
            return View(cveUpdateStaging);
        }

        // POST: Cve/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,ReleaseDate,ProductFamily,Product,Platform,Impact,MaxSeverity,Article,ArticleLink,Supercedence,Download,DownloadLink,BuildNumber,Details,DetailsLink,BaseScore,TemporalScore,CustomerActionRequired")] CveUpdateStaging cveUpdateStaging)
        {
            if (id != cveUpdateStaging.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(cveUpdateStaging);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CveUpdateStagingExists(cveUpdateStaging.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(cveUpdateStaging);
        }

        // GET: Cve/Delete/5
        public async Task<IActionResult> Delete(int? id)
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

        // POST: Cve/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var cveUpdateStaging = await _context.CveUpdateStagings.FindAsync(id);
            if (cveUpdateStaging != null)
            {
                _context.CveUpdateStagings.Remove(cveUpdateStaging);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool CveUpdateStagingExists(int id)
        {
            return _context.CveUpdateStagings.Any(e => e.Id == id);
        }
    }
}