using BankingManagmentApp.Data;
using BankingManagmentApp.Services.Forecasting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BankingManagmentApp.Controllers
{
    public class LoanRepaymentsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LoanRepaymentsController(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var repayments = await _context.LoanRepayments
                .Where(r => r.Loan.Customer.Id == userId)
                .OrderByDescending(x=>x.DueDate)
                .ToListAsync();

            foreach (var repayment in repayments)
            {
                if (repayment.DueDate < DateOnly.FromDateTime(DateTime.Today))
                {
                    repayment.Status = "Overdue"; // или друг статус
                    _context.Entry(repayment).Property(r => r.Status).IsModified = true;
                }
            }

            await _context.SaveChangesAsync();
            return View();
        }
    }
}
