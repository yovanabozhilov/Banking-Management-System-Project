using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using BankingManagmentApp.Data;
using BankingManagmentApp.Models;
using Microsoft.AspNetCore.Identity;

namespace BankingManagmentApp.Controllers
{
    public class AccountsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<Customers> _userManager;

        public AccountsController(ApplicationDbContext context, UserManager<Customers> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Accounts
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.Accounts.Include(a => a.Customer);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: Accounts/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var accounts = await _context.Accounts
                .Include(a => a.Customer)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (accounts == null)
            {
                return NotFound();
            }

            return View(accounts);
        }

        // GET: Accounts/Create
        public IActionResult Create()
        {
            ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "Id");
            return View();
        }

        // POST: Accounts/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("CustomerId,IBAN,AccountType,Balance,Currency,CreateAt,Status")] Accounts accounts)
        {
            var user = await _userManager.GetUserAsync(User);
            if (!ModelState.IsValid)
            {
                accounts.CustomerId = user.Id;
                accounts.IBAN = " ";
                accounts.AccountType = "User";
                accounts.Balance = 0;
                accounts.Currency = "BGN";
                accounts.CreateAt = DateTime.Now;
                accounts.Status = "Active";
                _context.Accounts.Add(accounts);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Index", "Profile");
            //ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "Id", accounts.CustomerId);
            //return View(accounts);
        }

        // GET: Accounts/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var accounts = await _context.Accounts.FindAsync(id);
            if (accounts == null)
            {
                return NotFound();
            }
            ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "Id", accounts.CustomerId);
            return View(accounts);
        }

        // POST: Accounts/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,CustomerId,IBAN,AccountType,Balance,Currency,CreateAt,Status")] Accounts accounts)
        {
            if (id != accounts.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(accounts);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AccountsExists(accounts.Id))
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
            ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "Id", accounts.CustomerId);
            return View(accounts);
        }

        // GET: Accounts/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var accounts = await _context.Accounts
                .Include(a => a.Customer)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (accounts == null)
            {
                return NotFound();
            }

            return View(accounts);
        }

        // POST: Accounts/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var accounts = await _context.Accounts.FindAsync(id);
            if (accounts != null)
            {
                _context.Accounts.Remove(accounts);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool AccountsExists(int id)
        {
            return _context.Accounts.Any(e => e.Id == id);
        }
    }
}
