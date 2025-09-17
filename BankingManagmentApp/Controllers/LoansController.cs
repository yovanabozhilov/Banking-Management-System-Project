using BankingManagmentApp.Data;
using BankingManagmentApp.Models;
using BankingManagmentApp.Services.Approval;
using DocumentFormat.OpenXml.Drawing.Charts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BankingManagmentApp.Controllers
{
    [Authorize]
    public class LoansController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<Customers> _userManager;

        public LoansController(ApplicationDbContext context, UserManager<Customers> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        
        // КЛИЕНТСКИ ДЕЙСТВИЯ
        

        // GET: Loans/Apply (форма за подаване на заявление)
        public IActionResult Apply()
        {
            return View();
        }

        // POST: Loans/Apply (подава заявлението + стартира автоматичния workflow)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Apply([Bind("Type,Amount,Term")] Loans loan,
                                               [FromServices] ILoanWorkflow workflow)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            if (!ModelState.IsValid)
            {
                // return View(loan);
                loan.CustomerId = user.Id;
                loan.Status = "Pending ";
                loan.Date = DateTime.UtcNow;
                

                if (loan.Term == default)
                    loan.Term = DateOnly.FromDateTime(DateTime.Today.AddMonths(12));
                
                
            }
            _context.Loans.Add(loan);
            await _context.SaveChangesAsync(); 

            await workflow.ProcessNewApplicationAsync(loan);

            return RedirectToAction("Index", "Profile");
        }

        // GET: Loans/MyLoans (списък на моите кредити)
        public async Task<IActionResult> MyLoans()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (User.IsInRole("Admin"))
            {
                var all = await _context.Loans
                    .Include(l => l.Customer)
                    .OrderByDescending(x => x.Date)
                    .ToListAsync();
                return View(all);
            }
            else
            {
                var mine = await _context.Loans
                    .Include(o => o.Customer)
                    .Where(x => x.CustomerId == _userManager.GetUserId(User))
                    .OrderByDescending(x => x.Date)
                    .ToListAsync();
                return View(mine);
            }
        }

        
        // АДМИН/ОБЩИ (преглед)
        

        // GET: Loans (общ преглед)
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (User.IsInRole("Admin"))
            {
                var list = await _context.Loans
                    .Include(l => l.Customer)
                    .Where(t => t.Status == "Pending"
                             || t.Status == "PendingReview"
                             || t.Status == "AutoApproved"
                             || t.Status == "AutoDeclined")
                    .OrderByDescending(x => x.Date)
                    .ToListAsync();

                return View(list);
            }
            else
            {
                var my = await _context.Loans
                    .Include(o => o.Customer)
                    .Where(x => x.CustomerId == _userManager.GetUserId(User))
                    .OrderByDescending(x => x.Date)
                    .ToListAsync();

                return View(my);
            }
        }

        // GET: Loans/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var loan = await _context.Loans
                .Include(l => l.Customer)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (loan == null) return NotFound();

            if (!User.IsInRole("Admin") && loan.CustomerId != _userManager.GetUserId(User))
                return Forbid();

            ViewBag.Repayments = await _context.LoanRepayments
                .Where(r => r.LoanId == loan.Id)
                .OrderBy(r => r.DueDate)
                .ToListAsync();

            return View(loan);
        }

        
        // АДМИН ДЕЙСТВИЯ
        

        // GET: Loans/Create (админ създава ръчно)
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "Email");
            return View();
        }

        // POST: Loans/Create (админ създава ръчно)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([Bind("Id,CustomerId,Type,Amount,Term,Date,Status,ApprovedAmount,ApprovalDate")] Loans loans)
        {
            if (!ModelState.IsValid)
            {
                ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "Email", loans.CustomerId);
                return View(loans);
            }

            _context.Add(loans);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: Loans/Edit/5
        [Authorize(Roles = "Admin")]
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
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,CustomerId,Type,Amount,Term,Date,Status,ApprovedAmount")] Loans loans)
        {

            if (id != loans.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                try
                {
                    loans.CustomerId = _userManager.GetUserId(User);

                    int termMonths = 0;
                    if (loans.Amount <= 1000)
                    {
                        termMonths = 12;
                    }
                    else if (loans.Amount <= 5000)
                    {
                        termMonths = 36;
                    }
                    else
                    {
                        termMonths = 60; 
                    }

                    DateTime calculatedEndDate = DateTime.Today.AddMonths(termMonths);
                    loans.Term = DateOnly.FromDateTime(calculatedEndDate);

                    LoanRepayments repayment = new LoanRepayments();
                    if (loans.Status == "Approved")
                    {
                        var hasExistingRepayments = await _context.LoanRepayments.AnyAsync(r => r.LoanId == loans.Id);

                        if (!hasExistingRepayments)
                        {
                            decimal monthlyPayment = loans.ApprovedAmount / termMonths;
                            var repayments = new List<LoanRepayments>();
                            DateOnly today = DateOnly.FromDateTime(DateTime.Today);
                            for (int i = 1; i <= termMonths; i++)
                            {
                                repayments.Add(new LoanRepayments
                                {
                                    LoanId = loans.Id,
                                    DueDate = today.AddMonths(i),
                                    AmountDue = monthlyPayment,
                                    AmountPaid = 0,
                                    PaymentDate =today,
                                    Status = "Pending" 
                                });
                            }
                            _context.LoanRepayments.AddRange(repayments);
                            await _context.SaveChangesAsync();
                        }
                    } 
                    _context.Loans.Update(loans);
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
            ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "Email", loans.CustomerId);
            return View(loans);
        }

        // GET: Loans/Delete/5
        [Authorize(Roles = "Admin")]
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
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var loans = await _context.Loans.FindAsync(id);
            if (loans != null)
            {
                var reps = _context.LoanRepayments.Where(r => r.LoanId == id);
                _context.LoanRepayments.RemoveRange(reps);

                var asses = _context.CreditAssessments.Where(a => a.LoanId == id);
                _context.CreditAssessments.RemoveRange(asses);

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
