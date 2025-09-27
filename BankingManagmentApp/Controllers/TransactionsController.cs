using BankingManagmentApp.Data;
using BankingManagmentApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace BankingManagmentApp.Controllers
{
    public class TransactionsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<Customers> _userManager;
        public TransactionsController(ApplicationDbContext context, UserManager<Customers> userManager)
        {
            _context = context;
            _userManager = userManager;
        }
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.Transactions.Include(t => t.Accounts);
            return View(await applicationDbContext.ToListAsync());
        }
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var transactions = await _context.Transactions
                .Include(t => t.Accounts)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (transactions == null)
            {
                return NotFound();
            }

            return View(transactions);
        }

        public IActionResult Create()
        {
            ViewData["AccountsId"] = new SelectList(_context.Accounts, "Id", "Id");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("AccountsId,TransactionType,Amount,Date,Description,ReferenceNumber")] Transactions transactions)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var account = await _context.Accounts
                .Include(a => a.Customer)
                .Where(a => a.Customer.Id == userId)
                .ToListAsync();

            if (account == null)
            {
                return Unauthorized();
            }

            var accountToUse = account.FirstOrDefault();
            if (!ModelState.IsValid)
            {
                if (transactions.TransactionType != "Credit")
                {
                    accountToUse = account.FirstOrDefault(a => a.Balance >= transactions.Amount);
                }
                else
                {
                    accountToUse = account.FirstOrDefault();
                    accountToUse.Balance += transactions.Amount;
                }

                if (accountToUse == null && transactions.TransactionType != "Credit")
                {
                    TempData["InsufficientFunds"] = "You do not have enough balance in your cards! Please try again later!";
                    return RedirectToAction("Index", "Profile");
                }
                else if (accountToUse != null && transactions.TransactionType != "Credit")
                {
                    accountToUse.Balance -= transactions.Amount;
                }
                transactions.AccountsId = accountToUse.Id;
                transactions.Date = DateOnly.FromDateTime(DateTime.Now);
                transactions.ReferenceNumber = new Random().Next(10000, 99999);
                _context.Transactions.Add(transactions);
                await _context.SaveChangesAsync();
                return RedirectToAction("Index", "Profile");
            }
            return View("Index", "Profile");
        }
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var transaction = await _context.Transactions
                .Include(t => t.Accounts)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (transaction == null) return NotFound();

            ViewData["AccountsId"] = new SelectList(_context.Accounts, "Id", "Id", transaction.AccountsId);
            return View(transaction);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,AccountsId,TransactionType,Amount,Date,Description,ReferenceNumber")] Transactions transactions)
        {
            if (id != transactions.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(transactions);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TransactionsExists(transactions.Id))
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
            ViewData["AccountsId"] = new SelectList(_context.Accounts, "Id", "Id", transactions.AccountsId);
            return View(transactions);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var transactions = await _context.Transactions
                .Include(t => t.Accounts)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (transactions == null)
            {
                return NotFound();
            }

            return View(transactions);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var transactions = await _context.Transactions.FindAsync(id);
            if (transactions != null)
            {
                _context.Transactions.Remove(transactions);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TransactionsExists(int id)
        {
            return _context.Transactions.Any(e => e.Id == id);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Pay(int repaymentId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var accounts = await _context.Accounts
            .Where(a => a.Customer.Id == userId)
            .ToListAsync();

            if (!accounts.Any())
            {
                return Unauthorized();
            }

            var repayment = await _context.LoanRepayments
                .FirstOrDefaultAsync(r => r.Id == repaymentId);

            if (repayment == null)
            {
                return NotFound();
            }

            var accountToUse = accounts.FirstOrDefault(a => a.Balance >= repayment.AmountDue);

            if (accountToUse == null)
            {
                TempData["InsufficientFunds"] = "You do not have enough balance in your cards! Please try again later!";
                return RedirectToAction("Index", "Profile");
            }
            else
            {
                accountToUse.Balance -= repayment.AmountDue;
            }
            repayment.Status = "Paid";
            repayment.PaymentDate = DateOnly.FromDateTime(DateTime.Now);

            var transaction = new Transactions
            {
                AccountsId = accountToUse.Id,
                TransactionType = "Debit",
                Amount = repayment.AmountDue,
                Date = DateOnly.FromDateTime(DateTime.Now),
                Description = $"Плащане на вноска #{repayment.Id}",
                ReferenceNumber = new Random().Next(10000, 99999)
            };

            _context.Transactions.Add(transaction);
            _context.Update(accountToUse);
            _context.Update(repayment);

            await _context.SaveChangesAsync();
            TempData["Success"] = "Success!";
            return RedirectToAction("Index", "Profile");
        }


    }
}