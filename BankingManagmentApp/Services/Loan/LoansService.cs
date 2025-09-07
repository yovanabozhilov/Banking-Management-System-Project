using BankingManagmentApp.Data;
using BankingManagmentApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BankingManagmentApp.Services
{
    public class LoansService
    {
        private readonly ApplicationDbContext _context;

        public LoansService(ApplicationDbContext context)
        {
            _context = context;
        }

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
    }
}
