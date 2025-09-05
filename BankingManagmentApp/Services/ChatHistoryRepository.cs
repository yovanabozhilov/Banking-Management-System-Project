using BankingManagmentApp.Data;
using BankingManagmentApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BankingManagmentApp.Services
{
    public class ChatHistoryRepository
    {
        private readonly ApplicationDbContext _context;

        public ChatHistoryRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task AddMessageAsync(ChatHistory history)
        {
            _context.ChatHistories.Add(history);
            await _context.SaveChangesAsync();
        }

        public async Task<List<ChatHistory>> GetUserHistoryAsync(string customerId)
        {
            return await _context.ChatHistories
                .Where(h => h.CustomerId == customerId)  // използваме CustomerId
                .OrderBy(h => h.Timestamp)
                .ToListAsync();                        // async ToList
        }
    }
}
