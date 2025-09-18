using BankingManagmentApp.Data;
using Microsoft.EntityFrameworkCore;

namespace BankingManagmentApp.Services;

public class KnowledgeBaseService
{
    private readonly ApplicationDbContext _db;
    public KnowledgeBaseService(ApplicationDbContext db) => _db = db;

    public async Task<List<TemplateAnswer>> SearchAsync(string query, int top = 3, CancellationToken ct = default)
    {
        query = (query ?? string.Empty).Trim();
        if (query.Length < 2) return new();

        var results = await _db.TemplateAnswer
            .Where(a => EF.Functions.Like(a.Keyword, $"%{query}%") || EF.Functions.Like(a.AnswerText, $"%{query}%"))
            .OrderByDescending(a => a.Id)
            .Take(top)
            .ToListAsync(ct);

        return results;
    }
}
