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

        // GET: Transactions
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.Transactions.Include(t => t.Accounts);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: Transactions/Details/5
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

        // GET: Transactions/Create
        public IActionResult Create()
        {
            ViewData["AccountsId"] = new SelectList(_context.Accounts, "Id", "Id");
            return View();
        }

        // POST: Transactions/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("AccountsId,TransactionType,Amount,Date,Description,ReferenceNumber")] Transactions transactions)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // намираме акаунта на този потребител
            var account = await _context.Accounts
                .Include(a => a.Customer)
                .FirstOrDefaultAsync(a => a.Customer.Id == userId);

            if (account == null)
            {
                return Unauthorized(); // ако няма акаунт за този user
            }


            if (!ModelState.IsValid)
            {
                transactions.AccountsId = account.Id;

                // попълваме автоматично някои стойности
                //transactions.TransactionType = "Debit";
                //var previousMonthDate = DateTime.Now.AddMonths(-1);
                //transactions.Date = DateOnly.FromDateTime(previousMonthDate);
                transactions.Date = DateOnly.FromDateTime(DateTime.Now);

                // пример за генериране на референтен номер (може да го смениш според логиката ти)
                transactions.ReferenceNumber = new Random().Next(10000, 99999);
                _context.Transactions.Add(transactions);
                await _context.SaveChangesAsync();
                return RedirectToAction("Index", "Profile");
            }

            //ViewData["AccountsId"] = new SelectList(_context.Accounts, "Id", "Id", transactions.AccountsId);
            return View("Index", "Profile");
        }
        // GET: Transactions/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var transactions = await _context.Transactions.FindAsync(id);
            if (transactions == null)
            {
                return NotFound();
            }
            // ViewData["AccountsId"] = new SelectList(_context.Accounts, "Id", "Id", transactions.AccountsId);
            return View("Index", "Profile");
        }

        // POST: Transactions/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
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

        // GET: Transactions/Delete/5
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

        // POST: Transactions/Delete/5
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

            // намираме акаунта на този user
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

            // проверка дали има достатъчен баланс
            var accountToUse = accounts.FirstOrDefault(a => a.Balance >= repayment.AmountDue);

            if (accountToUse == null)
            {
                // няма акаунт с достатъчен баланс – показваме alert
                TempData["InsufficientFunds"] = "You do not have enough balance in your cards! Please try again later!";
                return RedirectToAction("Index", "Profile");
            }
            else
            {
                // намаляме баланса
                accountToUse.Balance -= repayment.AmountDue;
            }
            // маркираме вноската като платена
            repayment.Status = "Paid";
            repayment.PaymentDate = DateOnly.FromDateTime(DateTime.Now);

            // записваме транзакцията
            var transaction = new Transactions
            {
                AccountsId = accountToUse.Id,
                TransactionType = "Loan Repayment",
                Amount = repayment.AmountDue,
                Date = DateOnly.FromDateTime(DateTime.Now),
                Description = $"Плащане на вноска #{repayment.Id}",
                ReferenceNumber = new Random().Next(10000, 99999)
            };

            _context.Transactions.Add(transaction);
            _context.Update(accountToUse);
            _context.Update(repayment);

            await _context.SaveChangesAsync();

            return RedirectToAction("Index", "Profile");
        }


    }
}




//        // GET: Transactions/Create
//        public IActionResult Pay()
//        {
//            ViewData["AccountsId"] = new SelectList(_context.Accounts, "Id", "Id");
//            return View();
//        }

//        [HttpPost]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> Pay([Bind("AccountsId,TransactionType,Amount,Date,Description,ReferenceNumber")] Transactions transactions)
//        {
//            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

//            // намираме акаунта на този потребител
//            var account = await _context.Accounts
//                .Include(a => a.Customer)
//                .FirstOrDefaultAsync(a => a.Customer.Id == userId);
//            LoanRepayments repay = new LoanRepayments();

//            if (account == null)
//            {
//                return Unauthorized(); // ако няма акаунт за този user
//            }


//            if (!ModelState.IsValid)
//            {
//                if (account.Balance > repay.AmountDue)
//                {
//                    transactions.AccountsId = account.Id;
//                    transactions.Date = DateOnly.FromDateTime(DateTime.Now);

//                    // пример за генериране на референтен номер (може да го смениш според логиката ти)
//                    transactions.ReferenceNumber = new Random().Next(10000, 99999);
//                    _context.Transactions.Add(transactions);
//                }
//                else
//                {

//                }
//                await _context.SaveChangesAsync();
//                return RedirectToAction("Index", "Profile");
//            }

//            //ViewData["AccountsId"] = new SelectList(_context.Accounts, "Id", "Id", transactions.AccountsId);
//            return View("Index", "Profile");
//        }


//    }

