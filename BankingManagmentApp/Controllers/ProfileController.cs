using BankingManagmentApp.Configuration;
using BankingManagmentApp.Data;
using BankingManagmentApp.Models;
using BankingManagmentApp.Services;
using BankingManagmentApp.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankingManagmentApp.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<Customers> _userManager;
        private readonly ICreditScoringService _scoring;

        private readonly CreditScoringOptions _creditOpts;

        public ProfileController(
            ApplicationDbContext db,
            UserManager<Customers> userManager,
            ICreditScoringService scoring,
            IOptions<CreditScoringOptions> creditOpts
        )
        {
            _db = db;
            _userManager = userManager;
            _scoring = scoring;
            _creditOpts = creditOpts?.Value ?? new CreditScoringOptions();
        }

        private IQueryable<Transactions> ApplyTxFilters(
            string userId,
            int? accountId, DateTime? from, DateTime? to, string? q, string? type)
        {
            var query = _db.Transactions
                .Include(t => t.Accounts)
                .Where(t => t.Accounts != null && t.Accounts.CustomerId == userId)
                .AsQueryable();

            if (accountId.HasValue)
                query = query.Where(t => t.AccountsId == accountId.Value);

            DateOnly? fromD = from.HasValue ? DateOnly.FromDateTime(from.Value.Date) : (DateOnly?)null;
            DateOnly? toD   = to.HasValue   ? DateOnly.FromDateTime(to.Value.Date)   : (DateOnly?)null;

            if (fromD.HasValue) query = query.Where(t => t.Date >= fromD.Value);
            if (toD.HasValue)   query = query.Where(t => t.Date <= toD.Value);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(t => t.Description != null && EF.Functions.Like(t.Description, $"%{term}%"));
            }

            if (!string.IsNullOrWhiteSpace(type))
            {
                var wanted = type.Trim().ToLower();
                query = query.Where(t => t.TransactionType != null &&
                                         t.TransactionType.ToLower() == wanted);
            }

            return query;
        }

        private string BuildCreditInfoMessage()
        {
            var flowsText   = _creditOpts.RequireBothFlows ? " with both inflow and outflow" : "";
            var monthsLabel = _creditOpts.MinActiveMonths == 1
                ? "1 active month"
                : $"{_creditOpts.MinActiveMonths} active months";

            return $"Credit score is not available yet. We need at least {_creditOpts.MinTransactions} transactions across at least {monthsLabel} within the last {_creditOpts.LookbackDays} days{flowsText}.";
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var accounts = await _db.Accounts
                .Where(a => a.CustomerId == user.Id)
                .OrderByDescending(a => a.CreateAt)
                .ToListAsync();

            var accountIds = accounts.Select(a => a.Id).ToList();

            var lastTx = await _db.Transactions
                .Where(t => accountIds.Contains(t.AccountsId))
                .OrderByDescending(t => t.Id)
                .Take(10)
                .ToListAsync();

            var loans = await _db.Loans
                .Where(l => l.CustomerId == user.Id)
                .OrderByDescending(l => l.Date)
                .ToListAsync();

            var loanIds = loans.Select(l => l.Id).ToList();

            var upcomingRepayments = await _db.LoanRepayments
                .OrderBy(r => r.DueDate)
                .Where(r => loanIds.Contains(r.LoanId) && (r.Status != "Paid" && r.Status != "Credit"))
                .Take(5)
                .ToListAsync();

            CreditAssessments? credit = null;
            var computed = await _scoring.ComputeAsync(user.Id);
            if (computed is not null)
            {
                credit = new CreditAssessments
                {
                    CreditScore = computed.Score,
                    RiskLevel = computed.RiskLevel,
                    Notes = computed.Notes
                };
            }

            var availableTypes = await _db.Transactions
                .Where(t => t.Accounts != null && t.Accounts.CustomerId == user.Id)
                .Select(t => t.TransactionType)
                .Distinct()
                .ToListAsync();

            var vm = new ProfileVm
            {
                User = user,
                Accounts = accounts,
                LastTransactions = lastTx,
                Loans = loans,
                UpcomingRepayments = upcomingRepayments,
                Credit = credit,
                AvailableTransactionTypes = availableTypes,
                CreditInfoMessage = credit is null ? BuildCreditInfoMessage() : null 
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> ExportAccounts(int? accountId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var query = _db.Accounts
                .Where(a => a.CustomerId == user.Id);

            if (accountId.HasValue)
            {
                var belongs = await _db.Accounts.AnyAsync(a => a.Id == accountId.Value &&
                                                               a.CustomerId == user.Id);
                if (!belongs) return Forbid();
                query = query.Where(a => a.Id == accountId.Value);
            }

            var accounts = await query.OrderBy(a => a.CreateAt).ToListAsync();

            var sb = new StringBuilder();
            sb.Append('\uFEFF'); 
            sb.AppendLine("IBAN,Type,Currency,Balance,Status,Created");

            var inv = CultureInfo.InvariantCulture;

            static string Esc(string s) =>
                string.IsNullOrEmpty(s) ? "" :
                s.Contains(',') || s.Contains('"') || s.Contains('\n')
                    ? "\"" + s.Replace("\"", "\"\"") + "\""
                    : s;

            foreach (var a in accounts)
            {
                sb.AppendLine(string.Join(",",
                    Esc(a.IBAN),
                    Esc(a.AccountType),
                    Esc(a.Currency),
                    a.Balance.ToString("0.00", inv),
                    Esc(a.Status ?? ""),
                    a.CreateAt.ToString("yyyy-MM-dd HH:mm:ss", inv)
                ));
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var fname = accountId.HasValue
                ? $"account_{accountId.Value}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv"
                : $"accounts_all_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";

            return File(bytes, "text/csv", fname);
        }

        [HttpGet]
        public async Task<IActionResult> ExportTransactions(int? accountId, DateTime? from, DateTime? to, string? q, string? type)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (accountId.HasValue)
            {
                var belongs = await _db.Accounts.AnyAsync(a => a.Id == accountId.Value && a.CustomerId == user.Id);
                if (!belongs) return Forbid();
            }

            var list = await ApplyTxFilters(user.Id, accountId, from, to, q, type)
                .OrderBy(t => t.Date)
                .ThenBy(t => t.Id)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.Append('\uFEFF'); 
            sb.AppendLine("Date,Type,Amount,Description,Reference,IBAN,Currency");

            var inv = CultureInfo.InvariantCulture;

            static string Esc(string s) =>
                string.IsNullOrEmpty(s) ? "" :
                s.Contains(',') || s.Contains('"') || s.Contains('\n')
                    ? "\"" + s.Replace("\"", "\"\"") + "\""
                    : s;

            foreach (var t in list)
            {
                var refText = (t.ReferenceNumber is int rn) ? rn.ToString(inv) : "";

                sb.AppendLine(string.Join(",",
                    t.Date.ToString("yyyy-MM-dd", inv),
                    Esc(t.TransactionType ?? ""),
                    t.Amount.ToString("0.00", inv),
                    Esc(t.Description ?? ""),
                    refText,
                    Esc(t.Accounts?.IBAN ?? ""),
                    Esc(t.Accounts?.Currency ?? "")
                ));
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var fname = "transactions";
            if (accountId.HasValue) fname += $"_acc{accountId.Value}";
            if (!string.IsNullOrWhiteSpace(type)) fname += $"_{type}";
            if (from.HasValue || to.HasValue)
            {
                var f = from?.ToString("yyyyMMdd") ?? "min";
                var tt = to?.ToString("yyyyMMdd") ?? "max";
                fname += $"_{f}-{tt}";
            }
            if (!string.IsNullOrWhiteSpace(q)) fname += "_desc";
            fname += $"_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";

            return File(bytes, "text/csv", fname);
        }

        [HttpGet]
        public async Task<IActionResult> ExportTransactionsPdf(int? accountId, DateTime? from, DateTime? to, string? q, string? type)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            Accounts? account = null;
            if (accountId.HasValue)
            {
                account = await _db.Accounts
                    .FirstOrDefaultAsync(a => a.Id == accountId.Value && a.CustomerId == user.Id);
                if (account is null) return Forbid();
            }

            var list = await ApplyTxFilters(user.Id, accountId, from, to, q, type)
                .OrderBy(t => t.Date)
                .ThenBy(t => t.Id)
                .ToListAsync();

            DateOnly? fromD = from.HasValue ? DateOnly.FromDateTime(from.Value.Date) : (DateOnly?)null;
            DateOnly? toD   = to.HasValue   ? DateOnly.FromDateTime(to.Value.Date)   : (DateOnly?)null;

            var doc = new BankingManagmentApp.Services.Pdf.TransactionsStatementPdf(user, account, list, fromD, toD);
            var bytes = doc.GeneratePdf();

            var fname = "transactions";
            if (accountId.HasValue) fname += $"_acc{accountId.Value}";
            if (!string.IsNullOrWhiteSpace(type)) fname += $"_{type}";
            if (from.HasValue || to.HasValue)
            {
                var f = from?.ToString("yyyyMMdd") ?? "min";
                var tt = to?.ToString("yyyyMMdd") ?? "max";
                fname += $"_{f}-{tt}";
            }
            if (!string.IsNullOrWhiteSpace(q)) fname += "_desc";
            fname += $"_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";

            return File(bytes, "application/pdf", fname);
        }

        [HttpGet]
        public async Task<IActionResult> SearchTransactions(int? accountId, DateTime? from, DateTime? to, string? q, string? type)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var accounts = await _db.Accounts
                .Where(a => a.CustomerId == user.Id)
                .OrderByDescending(a => a.CreateAt)
                .ToListAsync();

            var filteredTx = await ApplyTxFilters(user.Id, accountId, from, to, q, type)
                .OrderByDescending(t => t.Id)
                .Take(200)
                .ToListAsync();

            var loans = await _db.Loans
                .Where(l => l.CustomerId == user.Id)
                .OrderByDescending(l => l.Date)
                .ToListAsync();

            var loanIds = loans.Select(l => l.Id).ToList();

            var upcomingRepayments = await _db.LoanRepayments
                .OrderBy(r => r.DueDate)
                .Where(r => loanIds.Contains(r.LoanId) && (r.Status != "Paid" && r.Status != "Credit"))
                .Take(5)
                .ToListAsync();

            CreditAssessments? credit = null;
            var computed = await _scoring.ComputeAsync(user.Id);
            if (computed is not null)
            {
                credit = new CreditAssessments
                {
                    CreditScore = computed.Score,
                    RiskLevel = computed.RiskLevel,
                    Notes = computed.Notes
                };
            }

            var availableTypes = await _db.Transactions
                .Where(t => t.Accounts != null && t.Accounts.CustomerId == user.Id)
                .Select(t => t.TransactionType)
                .Distinct()
                .ToListAsync();

            var vm = new ProfileVm
            {
                User = user,
                Accounts = accounts,

                TransactionType = filteredTx,

                LastTransactions = await _db.Transactions
                    .Where(t => t.Accounts != null && t.Accounts.CustomerId == user.Id)
                    .OrderByDescending(t => t.Id)
                    .Take(10)
                    .ToListAsync(),

                Loans = loans,
                UpcomingRepayments = upcomingRepayments,
                Credit = credit,
                AvailableTransactionTypes = availableTypes,
                CreditInfoMessage = credit is null ? BuildCreditInfoMessage() : null 
            };

            return View("Index", vm);
        }

        [HttpGet]
        public IActionResult TransactionType(int? accountId, DateTime? from, DateTime? to, string type)
        {
            return RedirectToAction(nameof(SearchTransactions), new { accountId, from, to, type });
        }
    }
}
