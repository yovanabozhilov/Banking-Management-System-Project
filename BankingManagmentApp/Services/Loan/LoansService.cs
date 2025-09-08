using BankingManagmentApp.Data;
using BankingManagmentApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BankingManagmentApp.Services
{
    public class LoansService
    {
        private readonly ApplicationDbContext _context;

        public LoansService(ApplicationDbContext context) => _context = context;

        // Оставени както са при теб:
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

        // ---------- НОВО: унифицирано изчисление на статус ----------
        private static string CalcStatus(DateOnly due, decimal dueAmt, decimal paidAmt, DateOnly today)
        {
            if (paidAmt >= dueAmt && dueAmt > 0) return "Paid";
            if (due < today)                     return "Overdue";
            if (due == today)                    return "Due";
            return "Scheduled";
        }

        /// <summary>
        /// Преизчислява статусите на вноските (по избор само за конкретен потребител).
        /// Връща брой променени записи.
        /// </summary>
        public async Task<int> SyncRepaymentStatusesAsync(string? customerId = null, DateOnly? today = null)
        {
            var todayD = today ?? DateOnly.FromDateTime(DateTime.Today);

            var query = _context.LoanRepayments.AsQueryable();
            if (!string.IsNullOrEmpty(customerId))
            {
                // филтрираме по потребител през навигацията Loan → CustomerId
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

                    // ако вече е платена и няма дата на плащане — задаваме днес
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
