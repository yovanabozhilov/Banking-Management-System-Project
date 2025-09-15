//using BankingManagmentApp.Data;
//using BankingManagmentApp.Models;
//using SQLitePCL;

//namespace BankingManagmentApp.Services.Forecasting
//{
//    public class ForecastService
//    { 
//        private readonly ApplicationDbContext _context;
//        public ForecastService(ApplicationDbContext context)
//        {
//            _context = context;
//        }

//        public List<Transactions> GetHistoricalData()
//        {
//            return _context.Transactions
//                .OrderBy(t => t.Date)
//                .ToList();
//        }

//        public decimal GetForecast(List<Transactions> history, int window = 3)
//        {
//            return history
//                .OrderByDescending(x => x.Date)
//                .Take(window)
//                .Average(x => x.Amount);
//        }
//        public decimal GetSumForecast(List<Transactions> history)
//        {
//            if (history.Count < 2) return history.Sum(t => t.Amount);

//            // Проста линейна тенденция (разлика между последните два месеца)
//            var lastMonth = history[history.Count - 1];
//            var prevMonth = history[history.Count - 2];

//            var delta = lastMonth.Amount - prevMonth.Amount;
//            return lastMonth.Amount + delta; // прогноза за следващия месец
//        }

//        // 3. Прогноза на брой транзакции
//        public int GetCountForecast(List<Transactions> history)
//        {
//            if (history.Count < 2) return history.Count;

//            var lastMonthCount = history.Count(t => t.Date.Month == history[^1].Date.Month);
//            var prevMonthCount = history.Count(t => t.Date.Month == history[^2].Date.Month);

//            var delta = lastMonthCount - prevMonthCount;
//            return lastMonthCount + delta;
//        }

//        // 4. Откриване на аномалии (amount > 2 * средна стойност)
//        public List<Transactions> DetectAnomalies(List<Transactions> history)
//        {
//            if (!history.Any()) return new List<Transactions>();

//            var avg = history.Average(t => t.Amount);
//            return history.Where(t => t.Amount > avg * 2).ToList();
//        }
//    }
//}

//using BankingManagmentApp.Data;
//using BankingManagmentApp.Models;
using BankingManagmentApp.Data;
using BankingManagmentApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BankingManagmentApp.Services.Forecasting
{
    public class ForecastService
    {
        private readonly ApplicationDbContext _context;

        public ForecastService(ApplicationDbContext context)
        {
            _context = context;
        }

        // ================= TRANSACTIONS =================
        public Dictionary<string, int> ForecastTransactionVolumeMonthly()
        {
            return _context.Transactions
                .AsEnumerable() // DateOnly can't translate directly to SQL
                .GroupBy(t => t.Date.ToString("yyyy-MM"))
                .ToDictionary(g => g.Key, g => g.Count());
        }

        public decimal ForecastAvgTransactionValue()
        {
            return _context.Transactions.Any()
                ? _context.Transactions.Average(t => t.Amount)
                : 0;
        }

        public Dictionary<string, decimal> ForecastCashFlows()
        {
            return _context.Transactions
                .AsEnumerable()
                .GroupBy(t => t.Date.ToString("yyyy-MM"))
                .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));
        }

        public List<Transactions> DetectTransactionAnomalies()
        {
            decimal avg = ForecastAvgTransactionValue();
            return _context.Transactions
                .Where(t => t.Amount > avg * 3) // example anomaly: 3x average
                .ToList();
        }

        // ================= CARDS (Accounts) =================
        public decimal ForecastCardExpenses()
        {
            return _context.Accounts
                .Include(a => a.Transactions)
                .SelectMany(a => a.Transactions)
                .Where(t => t.TransactionType.ToLower().Contains("card"))
                .Sum(t => t.Amount);
        }

        public int ForecastActiveCardsCount()
        {
            return _context.Accounts.Count(a => a.Status.ToLower() == "Active");
        }

        public double ForecastCreditCardDefaultRisk()
        {
            var cardTx = _context.Accounts
                .Include(a => a.Transactions)
                .SelectMany(a => a.Transactions)
                .Where(t => t.TransactionType.ToLower().Contains("card"));

            if (!cardTx.Any()) return 0;
            int overdue = cardTx.Count(t => t.Amount < 0); // example: negative as risk
            return (double)overdue / cardTx.Count();
        }

        // ================= LOANS =================
        public double ForecastOverdueLoansRate()
        {
            var allRepayments = _context.LoanRepayments.ToList();
            if (!allRepayments.Any()) return 0;

            var overdueCount = allRepayments.Count(r => r.AmountPaid < r.AmountDue);
            return (double)overdueCount / allRepayments.Count;
        }

        public int ForecastNewLoans()
        {
            var oneMonthAgo = DateTime.Now.AddMonths(-1);
            return _context.Loans.Count(l => l.Date >= oneMonthAgo);
        }

        public string ForecastRepaymentTrend()
        {
            var repayments = _context.LoanRepayments.ToList();
            if (!repayments.Any()) return "No data";

            var overdueCount = repayments.Count(r => r.AmountPaid < r.AmountDue);
            double overdueRate = (double)overdueCount / repayments.Count;

            return overdueRate < 0.1 ? "Clients are repaying on time" : "Delay increasing";
        }

        // ================= CUSTOMERS =================
        public int ForecastNewCustomers()
        {
            var oneMonthAgo = DateTime.Now.AddMonths(-1);
            return _context.Users.Count(c => c.CreateAt >= oneMonthAgo);
        }

        public double ForecastChurnRate()
        {
            // simple churn example: inactive customers / total
            int total = _context.Users.Count();
            if (total == 0) return 0;
            int inactive = _context.Users.Count(c => !c.IsActive);
            return (double)inactive / total;
        }

    }
}

