using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using BankingManagmentApp.Data;
using BankingManagmentApp.Models;

namespace BankingManagmentApp.Controllers
{
    public class LoansController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LoansController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Loans
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.Loans.Include(l => l.Customer);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: Loans/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var loans = await _context.Loans
                .Include(l => l.Customer)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (loans == null)
            {
                return NotFound();
            }

            return View(loans);
        }

        // GET: Loans/Create
        public IActionResult Create()
        {
            ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "Id");
            return View();
        }

        // POST: Loans/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
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
            ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "Id", loans.CustomerId);
            return View(loans);
        }

        // GET: Loans/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var loans = await _context.Loans.FindAsync(id);
            if (loans == null)
            {
                return NotFound();
            }
            ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "Id", loans.CustomerId);
            return View(loans);
        }

        // POST: Loans/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,CustomerId,Type,Amount,Term,Date,Status,ApprovedAmount,ApprovalDate")] Loans loans)
        {
            if (id != loans.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(loans);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!LoansExists(loans.Id))
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
            ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "Id", loans.CustomerId);
            return View(loans);
        }

        // GET: Loans/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var loans = await _context.Loans
                .Include(l => l.Customer)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (loans == null)
            {
                return NotFound();
            }

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
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool LoansExists(int id)
        {
            return _context.Loans.Any(e => e.Id == id);
        }
    }
}
