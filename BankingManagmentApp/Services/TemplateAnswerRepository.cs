using BankingManagmentApp.Data;
using BankingManagmentApp.Models;
using System;
using System.Linq;

namespace BankingManagmentApp.Services
{
    public class TemplateAnswerRepository
    {
        private readonly ApplicationDbContext _context;

        public TemplateAnswerRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public TemplateAnswer? FindMatch(string input)
        {
            return _context.TemplateAnswers
                .FirstOrDefault(t => input.Contains(t.Keyword, StringComparison.OrdinalIgnoreCase));

        }
    }
}
