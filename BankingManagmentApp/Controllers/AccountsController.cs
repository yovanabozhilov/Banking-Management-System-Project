using BankingManagmentApp.Data;
using BankingManagmentApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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

        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.Accounts.Include(a => a.Customer);
            return View(await applicationDbContext.ToListAsync());
        }

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
        private string GenerateIBAN()
        {
            string countryCode = "BG";
            Random rnd = new Random();
            string checkDigits = rnd.Next(10, 99).ToString();

            string bankCode = "XXXX";

            string accountNumber = DateTime.Now.Ticks.ToString().Substring(0, 10);

            return $"{countryCode}{checkDigits}{bankCode}{accountNumber}";
        }

        public IActionResult Create()
        {
            ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "Id");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("CustomerId,IBAN,AccountType,Balance,Currency,CreateAt,Status")] Accounts accounts)
        {
            var user = await _userManager.GetUserAsync(User);
            if (!ModelState.IsValid)
            {
                accounts.CustomerId = user.Id;
                accounts.IBAN = GenerateIBAN();
                accounts.AccountType = "User";
                accounts.Balance = 0;
                accounts.Currency = "BGN";
                accounts.CreateAt = DateTime.Now;
                accounts.Status = "Pending";
                _context.Accounts.Add(accounts);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Index", "Profile");
        }

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
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,IBAN,AccountType,Balance,Currency,CreateAt,Status")] Accounts accounts)
        {
            if (id != accounts.Id)
                return NotFound();

            var existingAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == id);
            if (existingAccount == null)
                return NotFound();

            if (!ModelState.IsValid)
            {
                existingAccount.Status = accounts.Status;
                await _context.SaveChangesAsync();

            }
            _context.Accounts.Update(existingAccount);
            ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "Id", existingAccount.CustomerId);
            return RedirectToAction(nameof(Index));
        }



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
            return RedirectToAction("Index", "Profile");
        }

        private bool AccountsExists(int id)
        {
            return _context.Accounts.Any(e => e.Id == id);
        }
    }
}
