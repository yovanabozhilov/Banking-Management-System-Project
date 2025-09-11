using BankingManagmentApp.Data;
using Microsoft.AspNetCore.Mvc;

namespace BankingManagmentApp.Controllers
{
    public class AdminDashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminDashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var totalClients = _context.Customers.Count();
            var totalCredits = _context.Loans.Count();
            var totalCards = _context.Accounts.Count();


            var totalDeposits = _context.Transactions
                .Where(t => t.TransactionType == "Credit")
                .Sum(t => t.Amount);
            var totalWithdrawals = _context.Transactions
                .Where(t => t.TransactionType == "Debit")
                .Sum(t => t.Amount);



            var monthlyData = _context.Transactions
                .GroupBy(t => new { t.Date.Year, t.Date.Month })
                .Select(g => new
                {
                    Month = g.Key.Month,
                    Year = g.Key.Year,
                    Deposits = g.Where(x => x.TransactionType == "Credit").Sum(x => x.Amount),
                    Withdrawals = g.Where(x => x.TransactionType == "Debit").Sum(x => x.Amount),
                    
                })
                .OrderBy(g => g.Year).ThenBy(g => g.Month)
                .ToList();

            var ordered = monthlyData.OrderByDescending(m => new DateTime(m.Year, m.Month, 1)).ToList();
            if (ordered.Count >= 2)
            {
                var current = ordered[0];   // текущ месец
                var previous = ordered[1];  // предходен месец

                ViewBag.DepositsChange = ((float)(current.Deposits - previous.Deposits) / (float)previous.Deposits) * 100;
                ViewBag.WithdrawalsChange = ((float)(current.Withdrawals - previous.Withdrawals) / (double)previous.Withdrawals) * 100;
            }
            else
            {
                // ако няма два месеца данни, задаваме 0%
                ViewBag.ClientsChange = 0;
                ViewBag.CreditsChange = 0;
                ViewBag.DepositsChange = 0;
                ViewBag.WithdrawalsChange = 0;
               
            }

            ViewBag.TotalClients = totalClients;
            ViewBag.TotalCredits = totalCredits;
            ViewBag.TotalDeposits = totalDeposits;
            ViewBag.TotalWithdrawals = totalWithdrawals;
            ViewBag.MonthlyData = monthlyData;
            ViewBag.TotalCards = totalCards;

            var currentMonth = DateTime.Now.Month;
            var currentYear = DateTime.Now.Year;
            ViewBag.NewClientsThisMonth = _context.Customers
                .Count(c => c.CreateAt.Month == currentMonth && c.CreateAt.Year == currentYear);
            ViewBag.NewCardsMonthly = _context.Accounts.Count(x => x.CreateAt.Month == currentMonth && x.CreateAt.Year == currentYear);

            return View();
        }
    }
}
