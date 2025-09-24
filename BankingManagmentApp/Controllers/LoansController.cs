using BankingManagmentApp.Data;
using BankingManagmentApp.Models;
using BankingManagmentApp.Services;
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
        private readonly IEmailService _emailService;
        private readonly LoanContractGenerator _loanContractGenerator;
        public LoansController(ApplicationDbContext context, UserManager<Customers> userManager, IEmailService emailService, LoanContractGenerator loanContractGenerator)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
            _loanContractGenerator = loanContractGenerator;
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
                                              [FromServices] ILoanWorkflow workflow, List<IFormFile> documents)
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
            var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
            const long maxFileSize = 10 * 1024 * 1024; // 10 MB

            foreach (var file in documents)
            {
                if (file == null || file.Length == 0) continue;

                var extension = Path.GetExtension(file.FileName).ToLower();
                if (!allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError("documents", $"File {file.FileName} has unsupported format.");
                    continue;
                }

                if (file.Length > maxFileSize)
                {
                    ModelState.AddModelError("documents", $"File {file.FileName} exceeds 10 MB limit.");
                    continue;
                }
                if (documents.Count > 5)
                {
                    ModelState.AddModelError("documents", "You can upload a maximum of 5 files.");
                }

                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);

                var doc = new LoanApplication
                {
                    LoanId = loan.Id,
                    FileName = file.FileName,
                    ContentType = file.ContentType,
                    Data = ms.ToArray(),
                    UploadedAt = DateTime.UtcNow
                };

                _context.LoanApplication.Add(doc);
            }

            await _context.SaveChangesAsync();

            await workflow.ProcessNewApplicationAsync(loan);
            return RedirectToAction("Index", "Profile");
        }
        public async Task<IActionResult> DownloadDocument(int id)
        {
            var doc = await _context.LoanApplication.FindAsync(id);
            if (doc == null)
                return NotFound();

            return File(doc.Data, doc.ContentType, doc.FileName);
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


        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            List<Loans> list;
            if (User.IsInRole("Admin"))
            {
                list = await _context.Loans
                    .Include(l => l.Customer)
                    .Where(t => t.Status == "Pending" || t.Status == "PendingReview" ||
                                t.Status == "AutoApproved" || t.Status == "AutoDeclined")
                    .OrderByDescending(x => x.Date)
                    .ToListAsync();
            }
            else
            {
                list = await _context.Loans
                    .Include(o => o.Customer)
                    .Where(x => x.CustomerId == _userManager.GetUserId(User))
                    .OrderByDescending(x => x.Date)
                    .ToListAsync();
            }

            // Изчисляване на риска за визуализация
            foreach (var loan in list)
            {
                var accounts = await _context.Accounts
                    .Where(a => a.CustomerId == loan.CustomerId)
                    .ToListAsync();

                decimal totalBalance = accounts.Sum(a => a.Balance);

                var activeLoans = await _context.Loans
                    .Where(l => l.CustomerId == loan.CustomerId &&
                                (l.Status == "AutoApproved" || l.Status == "Approved"))
                    .ToListAsync();

                bool hasActiveLoan = activeLoans.Any();
                if (activeLoans.Count() > 5)
                {
                    hasActiveLoan = true;
                }
                bool isLargeLoan = loan.Amount >= 1000000;

                loan.IsRisky = (totalBalance < loan.Amount / 2 && loan.Amount < 1000000) || hasActiveLoan || isLargeLoan;
            }

            return View(list);
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

            var loanApplication = await _context.Loans
        .Include(l => l.Customer)
        .Include(l => l.LoanApplications)
        .FirstOrDefaultAsync(m => m.Id == id);

            ViewBag.Repayments = await _context.LoanRepayments
                .Where(r => r.LoanId == loan.Id)
                .OrderBy(r => r.DueDate)
                .ToListAsync();

            return View(loan);
        }


        // АДМИН ДЕЙСТВИЯ


        // GET: Loans/Create (админ създава ръчно)
        // [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "Email");
            return View();
        }

        // POST: Loans/Create (админ създава ръчно)
        [HttpPost]
        [ValidateAntiForgeryToken]
        // [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([Bind("Type,Amount,Term")] Loans loan,
                                              [FromServices] ILoanWorkflow workflow, List<IFormFile> documents)
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
            var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
            const long maxFileSize = 10 * 1024 * 1024; // 10 MB

            foreach (var file in documents)
            {
                if (file == null || file.Length == 0) continue;

                var extension = Path.GetExtension(file.FileName).ToLower();
                if (!allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError("documents", $"File {file.FileName} has unsupported format.");
                    continue;
                }

                if (file.Length > maxFileSize)
                {
                    ModelState.AddModelError("documents", $"File {file.FileName} exceeds 10 MB limit.");
                    continue;
                }
                if (documents.Count > 5)
                {
                    ModelState.AddModelError("documents", "You can upload a maximum of 5 files.");
                }

                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);

                var doc = new LoanApplication
                {
                    LoanId = loan.Id,
                    FileName = file.FileName,
                    ContentType = file.ContentType,
                    Data = ms.ToArray(),
                    UploadedAt = DateTime.UtcNow
                };

                _context.LoanApplication.Add(doc);
            }

            await _context.SaveChangesAsync();

            await workflow.ProcessNewApplicationAsync(loan);
            return RedirectToAction("Index", "Profile");
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
                    var existingLoan = await _context.Loans.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id);
                    loans.CustomerId = existingLoan.CustomerId;
                    //loans.CustomerId = _userManager.GetUserId(User);

                    if (existingLoan != null && existingLoan.Status != loans.Status)
                    {
                        var customer = await _userManager.FindByIdAsync(loans.CustomerId);
                        if (customer != null && !string.IsNullOrWhiteSpace(customer.Email))
                        {
                            // NEW LOGIC
                            // Generate the PDF contract only when the status becomes "Approved"
                            if (loans.Status == "Approved")
                            {
                                // Ensure the ApprovedAmount and ApprovalDate are set before generating
                                loans.ApprovedAmount = loans.ApprovedAmount > 0 ? loans.ApprovedAmount : loans.Amount;
                                loans.ApprovalDate = DateTime.UtcNow;

                                var pdfBytes = await _loanContractGenerator.GeneratePdfAsync(loans);

                                // Pass the PDF bytes to the email service
                                await _emailService.SendLoanStatusUpdateAsync(customer.Email, loans.Id, loans.Status, pdfBytes);
                            }
                            else
                            {
                                // For other status changes, send an email without an attachment
                                await _emailService.SendLoanStatusUpdateAsync(customer.Email, loans.Id, loans.Status, null);
                            }
                        }


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
                                        PaymentDate = today,
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

        [HttpGet]
        [Authorize] 
        public async Task<IActionResult> DownloadContract(int id)
        {
            var userId = _userManager.GetUserId(User);

            var loan = await _context.Loans
                .Include(l => l.Customer)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (loan is null)
                return NotFound();

            if (!User.IsInRole("Admin") && loan.CustomerId != userId)
                return Forbid();

            var st = (loan.Status ?? "").Trim().ToLowerInvariant();
            var isApproved = st == "approved" || st == "autoapproved";
            if (!isApproved)
                return NotFound();

            if (loan.ApprovalDate == default)
                loan.ApprovalDate = DateTime.UtcNow;
            if (loan.ApprovedAmount <= 0 && loan.Amount > 0)
                loan.ApprovedAmount = loan.Amount;

            var pdfBytes = await _loanContractGenerator.GeneratePdfAsync(loan);
            var fileName = $"Договор_Заем_{loan.Id}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
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
