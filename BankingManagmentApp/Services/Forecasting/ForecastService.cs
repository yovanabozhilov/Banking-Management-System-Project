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
                .AsEnumerable()
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
                .Where(t => t.Amount > avg * 3)
                .ToList();
        }

        // ================= CARDS (Accounts) =================
        public decimal ForecastCardExpenses()
        {
            return _context.Accounts
                .Include(a => a.Transactions)
                .SelectMany(a => a.Transactions)
                .Where(t => t.TransactionType.ToLower().Contains("Debit"))
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
                .Where(t => t.TransactionType.ToLower().Contains("Debit"));

            if (!cardTx.Any()) return 0;
            int overdue = cardTx.Count(t => t.Amount < 0); 
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
            int total = _context.Users.Count();
            if (total == 0) return 0;
            int inactive = _context.Users.Count(c => !c.IsActive);
            return (double)inactive / total;
        }

    }
}

