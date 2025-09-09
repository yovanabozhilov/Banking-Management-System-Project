using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using BankingManagmentApp.Data;
using BankingManagmentApp.Models;
using Microsoft.AspNetCore.Identity;
using System.Diagnostics.CodeAnalysis;

namespace BankingManagmentApp.Controllers
{
    public class LoansController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<Customers> _userManager;

        public LoansController(ApplicationDbContext context, UserManager<Customers> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Loans/Apply
        public IActionResult Apply()
        {
            return View();
        }

        // POST: Loans/Apply
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Apply([Bind("Type,Amount,Term")] Loans loan)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (!ModelState.IsValid)
            {
                loan.CustomerId = user.Id;
                loan.Status = "Pending";
                loan.Date = DateTime.Now;
                loan.Amount = loan.Amount;
                //loan.Type = "credit";

                _context.Loans.Add(loan);
                await _context.SaveChangesAsync();

                // Redirect to Profile dashboard (loans will show there)
            }
            return RedirectToAction("Index", "Profile");
        }

        // GET: Loans/MyLoans
        public async Task<IActionResult> MyLoans()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (User.IsInRole("Admin"))
            {
                var myDbContext = _context.Loans
                .Include(l => l.Customer);
                return View(await myDbContext.ToListAsync());
            }
            else
            {
                var myGardenDbContext = _context.Loans
                    .Include(o => o.Customer)
                    .Where(x => x.CustomerId == _userManager.GetUserId(User));
                return View(await myGardenDbContext.ToListAsync());
            }
        }

        // ============================
        // ADMIN FEATURES (Scaffolded)
        // ============================

        // GET: Loans
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (User.IsInRole("Admin"))
            {
                var myDbContext = _context.Loans
                .Include(l => l.Customer)
                .Where(t => t.Status == "Pending");
                return View(await myDbContext.ToListAsync());
            }
            else
            {
                var myGardenDbContext = _context.Loans
                    .Include(o => o.Customer)
                    .Where(x => x.CustomerId == _userManager.GetUserId(User));
                return View(await myGardenDbContext.ToListAsync());
            }
        }

        // GET: Loans/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var loans = await _context.Loans
                .Include(l => l.Customer)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (loans == null) return NotFound();

            return View(loans);
        }

        // GET: Loans/Create (admin creates manually)
        public IActionResult Create()
        {
            ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "Email");
            return View();
        }

        // POST: Loans/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,CustomerId,Type,Amount,Term,Date,Status,ApprovedAmount,ApprovalDate")] Loans loans)
        {
            if (ModelState.IsValid)
            {
                _context.Add(loans);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "Email", loans.CustomerId);
            return View(loans);
        }

        // GET: Loans/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var loans = await _context.Loans.FindAsync(id);
            if (loans == null) return NotFound();

            ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "Email", loans.CustomerId);
            return View(loans);
        }

        // POST: Loans/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,CustomerId,Type,Amount,Term,Date,Status,ApprovedAmount,ApprovalDate")] Loans loans)
        {
            if (id != loans.Id) return NotFound();
            if (_userManager.GetUserId(User) == null)
            {
                return NotFound();
            }
            if (!ModelState.IsValid)
            {
                try
                {
                    loans.CustomerId = _userManager.GetUserId(User);
                    loans.ApprovalDate = DateTime.Now;
                    _context.Loans.Update(loans);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!LoansExists(loans.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "Email", loans.CustomerId);
            return View(loans);
        }

        // GET: Loans/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var loans = await _context.Loans
                .Include(l => l.Customer)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (loans == null) return NotFound();

            return View(loans);
        }

        // POST: Loans/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var loans = await _context.Loans.FindAsync(id);
            if (loans != null)
            {
                _context.Loans.Remove(loans);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool LoansExists(int id)
        {
            return _context.Loans.Any(e => e.Id == id);
        }
    }
}
