using BankingManagmentApp.Data;
using Microsoft.AspNetCore.Mvc;

using BankingManagmentApp.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Globalization;


namespace BankingManagmentApp.Controllers
{
    public class AdminDashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminDashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Benchmarking()
        {
            // 1) load distinct TransactionType values
            var distinctTypes = await _context.Transactions
                .Select(t => t.TransactionType)
                .Where(t => t != null)
                .Distinct()
                .ToListAsync();
        
            // detect common pattern: are TransactionType values just "Credit"/"Debit"?
            var lowerTypes = distinctTypes.Select(s => s?.Trim().ToLower()).Where(s => s != null).ToList();
            bool hasCreditDebit = lowerTypes.Contains("credit") || lowerTypes.Contains("debit");
        
            // detect whether amounts can be negative in your DB (used for fallback inference)
            bool hasNegativeAmounts = await _context.Transactions.AnyAsync(t => t.Amount < 0);
        
            // 2) aggregate by categoryKey (we use TransactionType as the categoryKey for now)
            //    and compute Income / Expense depending on detected pattern.
            var grouped = await _context.Transactions
                .GroupBy(t => t.TransactionType)
                .Select(g => new
                {
                    Category = g.Key ?? "Unknown",
                    // if DB uses Credit/Debit words, treat Credit as income, Debit as expense
                    IncomeWhenCredit = g.Where(x => x.TransactionType != null && x.TransactionType.ToLower() == "credit").Sum(x => (decimal?)x.Amount) ?? 0m,
                    ExpenseWhenDebit = g.Where(x => x.TransactionType != null && x.TransactionType.ToLower() == "debit").Sum(x => (decimal?)x.Amount) ?? 0m,
                    // fallback: positive amounts income, negative amounts expense (if negative exist)
                    IncomePos = hasNegativeAmounts ? g.Where(x => x.Amount > 0).Sum(x => (decimal?)x.Amount) ?? 0m : 0m,
                    ExpenseNeg = hasNegativeAmounts ? g.Where(x => x.Amount < 0).Sum(x => (decimal?)x.Amount) ?? 0m : 0m,
                    // fallback total
                    Total = g.Sum(x => (decimal?)x.Amount) ?? 0m
                })
                .ToListAsync();
        
            // 3) fake industry benchmarks (you can tune values here)
            var industryBenchmarks = new Dictionary<string, (decimal Income, decimal Expense)>(StringComparer.OrdinalIgnoreCase)
            {
                // some example / dummy industry numbers - edit to taste
                { "Credit", (Income: 50000m, Expense: 0m) },
                { "Debit",  (Income: 0m, Expense: 42000m) },
                { "Groceries", (Income: 0m, Expense: 12000m) },
                { "Rent", (Income: 0m, Expense: 8000m) },
                { "Salary", (Income: 38000m, Expense: 0m) },
                { "Utilities", (Income: 0m, Expense: 2500m) },
                { "Unknown", (Income: 10000m, Expense: 10000m) } // default fallback
            };
        
            // compute fallback averages for unknown categories (so we have something reasonable)
            var avgIndustryIncome = industryBenchmarks.Values.Average(v => v.Income);
            var avgIndustryExpense = industryBenchmarks.Values.Average(v => v.Expense);
        
            // 4) map to VM, selecting the right Income/Expense based on detection
            var vmList = grouped.Select(g =>
            {
                decimal income = 0m, expense = 0m;
        
                if (hasCreditDebit)
                {
                    // when app uses Credit/Debit, Income for "Credit" rows comes from IncomeWhenCredit, Expense for "Debit" from ExpenseWhenDebit
                    income = g.IncomeWhenCredit;
                    expense = g.ExpenseWhenDebit;
                }
                else if (hasNegativeAmounts)
                {
                    // when amounts carry sign, use positive/negative partitioning per-category
                    income = g.IncomePos;
                    expense = Math.Abs(g.ExpenseNeg);
                }
                else
                {
                    // fallback: treat the grouped Total as "Income" (no sign info); expense = 0.
                    income = g.Total;
                    expense = 0m;
                }
        
                // find industry benchmark or use averages
                var catKey = string.IsNullOrWhiteSpace(g.Category) ? "Unknown" : g.Category;
                (decimal IndustryIncome, decimal IndustryExpense) bench;
                if (!industryBenchmarks.TryGetValue(catKey, out var b))
                {
                    bench = (avgIndustryIncome, avgIndustryExpense);
                }
                else
                {
                    bench = (b.Income, b.Expense);
                }
        
                return new CategoryBenchmarkVm
                {
                    Category = catKey,
                    Income = Math.Round(income, 2),
                    Expense = Math.Round(expense, 2),
                    IndustryAvgIncome = Math.Round(bench.IndustryIncome, 2),
                    IndustryAvgExpense = Math.Round(bench.IndustryExpense, 2)
                };
            })
            .OrderByDescending(x => x.Expense + x.Income)
            .ToList();
        
            // Pass an informational flag to view via ViewBag so the UI can explain the chosen mode
            ViewBag.BenchmarkMode = hasCreditDebit ? "Detected Credit/Debit as direction (comparing Credit vs Debit)" :
                                 hasNegativeAmounts ? "Detected signed amounts (positive = income, negative = expense)" :
                                 "Fallback mode: no direction field detected (treating totals as Income)";
        
            return View(vmList);
        }


        public async Task<IActionResult> CategoryComparison()
        {
            var data = await _context.Transactions
                .GroupBy(t => t.TransactionType)
                .Select(g => new CategoryComparisonVm
                {
                    Category = g.Key,
                    Income = g.Key == "Credit" ? g.Sum(x => x.Amount) : 0,
                    Expense = g.Key == "Debit" ? g.Sum(x => x.Amount) : 0
                })
                .ToListAsync();

            return View(data);
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
