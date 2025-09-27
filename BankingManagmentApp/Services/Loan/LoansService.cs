using BankingManagmentApp.Data;
using BankingManagmentApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BankingManagmentApp.Services
{
    public class LoansService
    {
        private readonly ApplicationDbContext _context;

        public LoansService(ApplicationDbContext context) => _context = context;
        public async Task<Loans> ApplyAsync(string customerId, string type, decimal amount, DateOnly term)
        {
            var loan = new Loans
            {
                CustomerId = customerId,
                Type = type,
                Amount = amount,
                Term = term,
                Status = "Pending"
            };

            _context.Loans.Add(loan);
            await _context.SaveChangesAsync();
            return loan;
        }

        public async Task<List<Loans>> GetCustomerLoansAsync(string customerId)
        {
            return await _context.Loans
                .Where(l => l.CustomerId == customerId)
                .ToListAsync();
        }
        private static string CalcStatus(DateOnly due, decimal dueAmt, decimal paidAmt, DateOnly today)
        {
            if (paidAmt >= dueAmt && dueAmt > 0) return "Paid";
            if (due < today)                     return "Overdue";
            if (due == today)                    return "Due";
            return "Scheduled";
        }
        public async Task<int> SyncRepaymentStatusesAsync(string? customerId = null, DateOnly? today = null)
        {
            var todayD = today ?? DateOnly.FromDateTime(DateTime.Today);

            var query = _context.LoanRepayments.AsQueryable();
            if (!string.IsNullOrEmpty(customerId))
            {
                query = query.Where(r => r.Loan != null && r.Loan.CustomerId == customerId);
            }

            var reps = await query.ToListAsync();
            var changed = 0;

            foreach (var r in reps)
            {
                var newStatus = CalcStatus(r.DueDate, r.AmountDue, r.AmountPaid, todayD);

                if (!string.Equals(newStatus, r.Status, StringComparison.OrdinalIgnoreCase))
                {
                    r.Status = newStatus;
                    if (newStatus == "Paid" && r.PaymentDate == null)
                        r.PaymentDate = todayD;

                    changed++;
                }
            }

            if (changed > 0) await _context.SaveChangesAsync();
            return changed;
        }
    }
}
