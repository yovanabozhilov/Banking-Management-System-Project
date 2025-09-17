using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BankingManagmentApp.Data;
using Microsoft.EntityFrameworkCore;

namespace BankingManagmentApp.Services
{
    public class ChatTools
    {
        private readonly ApplicationDbContext _db;
        public ChatTools(ApplicationDbContext db) => _db = db;

        public async Task<decimal> GetBalanceAsync(string userId, CancellationToken ct = default)
        {
            return await _db.Accounts
                .Where(a => a.CustomerId == userId)
                .Select(a => (decimal?)a.Balance)
                .SumAsync(ct) ?? 0m;
        }

        public async Task<List<object>> GetRecentTransactionsAsync(string userId, int count = 5, CancellationToken ct = default)
        {
            var q =
                from t in _db.Transactions.Include(t => t.Accounts)
                where t.Accounts.CustomerId == userId
                orderby t.Id descending
                select new
                {
                    t.Id,
                    Date = (DateTime?)null,
                    t.Amount,
                    Type = t.TransactionType,
                    t.Description,
                    AccountIban = t.Accounts.IBAN
                };

            var list = await q.Take(count).ToListAsync(ct);
            return list.Cast<object>().ToList();
        }

        public async Task<string> GetLoanStatusAsync(string userId, CancellationToken ct = default)
        {
            var loan = await _db.Loans
                .Where(l => l.CustomerId == userId)
                .OrderByDescending(l => l.Id)  
                .FirstOrDefaultAsync(ct);

            if (loan is null)
                return "No active applications found.";

            var status = loan.Status; 
            return $"{loan.Type} - {(string.IsNullOrWhiteSpace(status) ? "Unknown" : status)}";
        }
    }
}
