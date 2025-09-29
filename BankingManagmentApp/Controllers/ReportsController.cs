using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using BankingManagmentApp.Data;
using BankingManagmentApp.ViewModels.Reports;
using BankingManagmentApp.Services.Pdf;
using BankingManagmentApp.Services.Excel;
using BankingManagmentApp.Services;
using QuestPDF.Fluent;

namespace BankingManagmentApp.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IEmailService _emailService;

        public ReportsController(ApplicationDbContext db, IEmailService emailService)
        {
            _db = db;
            _emailService = emailService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var today = DateTime.Today;

            var filters = new ReportFilterVm
            {
                From = new DateOnly(today.Year, today.Month, 1).AddMonths(-11),
                To = DateOnly.FromDateTime(today),
                GroupBy = ReportGroupBy.Monthly
            };

            await PopulateAccountsSelect(filters);

            ViewBag.ShowResults = false;

            return View(new ReportResultVm
            {
                Filters = filters,
                Rows = new List<ReportRow>()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index([Bind(Prefix = "Filters")] ReportFilterVm filters)
        {
            NormalizeFilters(filters);
            await PopulateAccountsSelect(filters);

            var vm = await BuildReport(filters);
            ViewBag.ShowResults = true;

            if (Request.Headers.TryGetValue("X-Requested-With", out var xrw) &&
                string.Equals(xrw, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            {
                return PartialView("_ReportTable", vm);
            }
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportPdf([Bind(Prefix = "Filters")] ReportFilterVm filters)
        {
            NormalizeFilters(filters);
            await PopulateAccountsSelect(filters);
            var vm = await BuildReport(filters);

            var fileName = $"FinancialReport_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
            var emailSubject = $"Financial Report {fileName}";
            var emailBody = "Please find your requested financial report attached.";
            var userEmail = User.Identity?.Name;

            var pdfDoc = new FinancialReportPdf(vm);
            byte[] bytes = pdfDoc.GeneratePdf();

            if (!string.IsNullOrEmpty(userEmail))
            {
                await _emailService.SendEmailWithAttachmentAsync(
                    userEmail, emailSubject, emailBody, bytes, fileName);
            }

            return File(bytes, "application/pdf", fileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportExcel([Bind(Prefix = "Filters")] ReportFilterVm filters)
        {
            NormalizeFilters(filters);
            await PopulateAccountsSelect(filters);
            var vm = await BuildReport(filters);

            var fileName = $"FinancialReport_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            var emailSubject = $"Financial Report {fileName}";
            var emailBody = "Please find your requested financial report attached.";
            var userEmail = User.Identity?.Name;

            var bytes = FinancialReportExcel.Build(vm);
            if (!string.IsNullOrEmpty(userEmail))
            {
                await _emailService.SendEmailWithAttachmentAsync(userEmail, emailSubject, emailBody, bytes, fileName);
            }
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        private static void NormalizeFilters(ReportFilterVm filters)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            filters.To ??= today;
            filters.From ??= new DateOnly(today.Year, today.Month, 1).AddMonths(-11);

            if (filters.From > filters.To)
            {
                var tmp = filters.From;
                filters.From = filters.To;
                filters.To = tmp;
            }

            if (filters.GroupBy != ReportGroupBy.Monthly && filters.GroupBy != ReportGroupBy.Yearly)
                filters.GroupBy = ReportGroupBy.Monthly;

            if (!string.IsNullOrWhiteSpace(filters.CustomerId))
                filters.CustomerId = filters.CustomerId!.Trim();

            if (!string.IsNullOrWhiteSpace(filters.CustomerName))
                filters.CustomerName = filters.CustomerName!.Trim();
        }

        private async Task PopulateAccountsSelect(ReportFilterVm filters)
        {
            var accs = await _db.Accounts
                .OrderBy(a => a.IBAN)
                .Select(a => new SelectListItem
                {
                    Value = a.Id.ToString(),
                    Text = a.IBAN + " (" + a.Currency + ")"
                })
                .ToListAsync();

            filters.AccountsSelect = new[]
            {
                new SelectListItem { Value = "", Text = "All accounts" }
            }.Concat(accs);

            if (filters.AccountId.HasValue)
            {
                var label = await _db.Accounts.Where(a => a.Id == filters.AccountId.Value)
                    .Select(a => a.IBAN + " (" + a.Currency + ")")
                    .FirstOrDefaultAsync();

                filters.SelectedAccountLabel = label ?? "All accounts";
            }
            else
            {
                filters.SelectedAccountLabel = "All accounts";
            }
        }

        private async Task<ReportResultVm> BuildReport(ReportFilterVm filters)
        {
            var q = _db.Transactions
                .AsNoTracking()
                .Include(t => t.Accounts)
                    .ThenInclude(a => a.Customer)
                .AsQueryable();

            if (filters.AccountId.HasValue)
                q = q.Where(t => t.AccountsId == filters.AccountId.Value);

            if (filters.From.HasValue)
                q = q.Where(t => t.Date >= filters.From.Value);

            if (filters.To.HasValue)
                q = q.Where(t => t.Date <= filters.To.Value);

            if (!string.IsNullOrWhiteSpace(filters.CustomerId))
                q = q.Where(t => t.Accounts.CustomerId == filters.CustomerId);

            if (!string.IsNullOrWhiteSpace(filters.CustomerName))
            {
                var name = filters.CustomerName!.ToLower();
                q = q.Where(t =>
                    (t.Accounts.Customer.FirstName + " " + t.Accounts.Customer.LastName).ToLower().Contains(name) ||
                    t.Accounts.Customer.UserName.ToLower().Contains(name) ||
                    t.Accounts.Customer.Email.ToLower().Contains(name));
            }

            var uniqueCustomers = await q
                .Select(t => new
                {
                    t.Accounts.CustomerId,
                    t.Accounts.Customer.FirstName,
                    t.Accounts.Customer.LastName
                })
                .Distinct()
                .ToListAsync();

            string? selectedCustId = null;
            string? selectedCustName = null;
            if (uniqueCustomers.Count == 1)
            {
                selectedCustId = uniqueCustomers[0].CustomerId;
                selectedCustName = $"{uniqueCustomers[0].FirstName} {uniqueCustomers[0].LastName}".Trim();
            }

            var rows = filters.GroupBy == ReportGroupBy.Monthly
                ? await q
                    .GroupBy(t => new { t.Date.Year, t.Date.Month })
                    .Select(g => new ReportRow
                    {
                        Year = g.Key.Year,
                        Month = g.Key.Month,
                        TotalTransactions = g.Count(),
                        TotalAmount = g.Sum(x => x.Amount)
                    })
                    .ToListAsync()
                : await q
                    .GroupBy(t => new { t.Date.Year })
                    .Select(g => new ReportRow
                    {
                        Year = g.Key.Year,
                        Month = null,
                        TotalTransactions = g.Count(),
                        TotalAmount = g.Sum(x => x.Amount)
                    })
                    .ToListAsync();

            if (filters.GroupBy == ReportGroupBy.Monthly)
            {
                var typeSums = await q
                    .GroupBy(t => new { t.Date.Year, t.Date.Month, t.TransactionType })
                    .Select(g => new
                    {
                        g.Key.Year,
                        g.Key.Month,
                        Type = g.Key.TransactionType ?? "Unknown",
                        Total = g.Sum(x => x.Amount)
                    })
                    .ToListAsync();

                foreach (var r in rows)
                {
                    r.AmountByType = typeSums
                        .Where(ts => ts.Year == r.Year && ts.Month == r.Month)
                        .ToDictionary(ts => ts.Type, ts => ts.Total, StringComparer.OrdinalIgnoreCase);
                }
            }
            else
            {
                var typeSums = await q
                    .GroupBy(t => new { t.Date.Year, t.TransactionType })
                    .Select(g => new
                    {
                        g.Key.Year,
                        Type = g.Key.TransactionType ?? "Unknown",
                        Total = g.Sum(x => x.Amount)
                    })
                    .ToListAsync();

                foreach (var r in rows)
                {
                    r.AmountByType = typeSums
                        .Where(ts => ts.Year == r.Year)
                        .ToDictionary(ts => ts.Type, ts => ts.Total, StringComparer.OrdinalIgnoreCase);
                }
            }

            if (filters.GroupBy == ReportGroupBy.Monthly)
            {
                var flows = await q
                    .GroupBy(t => new { t.Date.Year, t.Date.Month })
                    .Select(g => new
                    {
                        g.Key.Year,
                        g.Key.Month,
                        Credits = g.Where(x => (x.TransactionType ?? "") == "Credit").Sum(x => (decimal?)x.Amount) ?? 0m,
                        Debits  = g.Where(x => (x.TransactionType ?? "") == "Debit").Sum(x => (decimal?)x.Amount) ?? 0m
                    })
                    .ToListAsync();

                foreach (var r in rows)
                {
                    var f = flows.FirstOrDefault(x => x.Year == r.Year && x.Month == r.Month);
                    if (f != null) r.NetFlow = f.Credits - f.Debits;
                }
            }
            else
            {
                var flows = await q
                    .GroupBy(t => new { t.Date.Year })
                    .Select(g => new
                    {
                        g.Key.Year,
                        Credits = g.Where(x => (x.TransactionType ?? "") == "Credit").Sum(x => (decimal?)x.Amount) ?? 0m,
                        Debits  = g.Where(x => (x.TransactionType ?? "") == "Debit").Sum(x => (decimal?)x.Amount) ?? 0m
                    })
                    .ToListAsync();

                foreach (var r in rows)
                {
                    var f = flows.FirstOrDefault(x => x.Year == r.Year);
                    if (f != null) r.NetFlow = f.Credits - f.Debits;
                }
            }

            var totalsByTypeList = await q
                .GroupBy(t => t.TransactionType ?? "Unknown")
                .Select(g => new { Type = g.Key, Total = g.Sum(x => x.Amount) })
                .ToListAsync();

            var totalsByType = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (var x in totalsByTypeList)
                totalsByType[x.Type] = x.Total;

            var descRaw = await q
                .Select(t => new
                {
                    Type = t.TransactionType ?? "Unknown",
                    Desc = t.Description ?? "",
                    Amount = t.Amount
                })
                .ToListAsync();

            var descGrouped = descRaw
                .Select(x => new
                {
                    x.Type,
                    Key = NormalizeKeyword(x.Desc),  
                    x.Amount
                })
                .GroupBy(x => new { x.Type, x.Key })
                .Select(g => new
                {
                    g.Key.Type,
                    Key = g.Key.Key,
                    Total = g.Sum(z => z.Amount)
                })
                .ToList();

            var topDescDict = new Dictionary<string, List<DescriptionAggVm>>(StringComparer.OrdinalIgnoreCase);
            foreach (var grp in descGrouped.GroupBy(x => x.Type))
            {
                var list = grp
                    .OrderByDescending(x => x.Total)
                    .Take(5)
                    .Select(x => new DescriptionAggVm
                    {
                        Description = ToTitleCaseSafe(x.Key),
                        Total = x.Total
                    })
                    .ToList();

                topDescDict[grp.Key] = list;
            }

            var totalCredits = await q.Where(t => (t.TransactionType ?? "") == "Credit")
                .SumAsync(t => (decimal?)t.Amount) ?? 0m;
            var totalDebits = await q.Where(t => (t.TransactionType ?? "") == "Debit")
                .SumAsync(t => (decimal?)t.Amount) ?? 0m;

            return new ReportResultVm
            {
                Filters = filters,
                Rows = (filters.GroupBy == ReportGroupBy.Monthly
                    ? rows.OrderBy(x => x.Year).ThenBy(x => x.Month ?? 0)
                    : rows.OrderBy(x => x.Year)).ToList(),
                TotalsByType = totalsByType,
                TopDescriptionsByType = topDescDict,
                SelectedCustomerId = selectedCustId,
                SelectedCustomerName = selectedCustName,
                TotalCredits = totalCredits,
                TotalDebits = totalDebits
            };
        }

        private static readonly Dictionary<string, string> KeywordSynonyms =
            new(StringComparer.OrdinalIgnoreCase)
        {
            ["payment"] = "pay",
            ["payments"] = "pay",
            ["paid"] = "pay",
            ["paying"] = "pay",
            ["payout"] = "pay",
            ["payouts"] = "pay",

            ["salary"] = "salary",
            ["salaries"] = "salary",
            ["wage"] = "salary",
            ["wages"] = "salary",

            ["deposit"] = "deposit",
            ["deposits"] = "deposit",
            ["withdraw"] = "withdraw",
            ["withdrawing"] = "withdraw",
            ["withdrawn"] = "withdraw",
            ["withdrawal"] = "withdraw",
            ["withdrawals"] = "withdraw",
            ["withdrawl"] = "withdraw",
            ["withdrawls"] = "withdraw",

            ["transfer"] = "transfer",
            ["transfers"] = "transfer",

            ["fee"] = "fee",
            ["fees"] = "fee"
        };

        private static readonly HashSet<string> Stopwords =
            new(StringComparer.OrdinalIgnoreCase)
        {
            "the","a","an","to","for","from","of","and","on","in","at","by","via","with"
        };

        private static string NormalizeKeyword(string? s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return "(no description)";

            var t = s.Trim().ToLowerInvariant();
            t = Regex.Replace(t, @"[^\p{L}\p{Nd}]+", " ");
            t = Regex.Replace(t, @"\s+", " ").Trim();

            if (t.Length == 0)
                return "(no description)";

            var tokens = t.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                          .Where(tok => !Stopwords.Contains(tok))
                          .Select(tok => KeywordSynonyms.TryGetValue(tok, out var canon) ? canon : tok)
                          .ToList();

            if (tokens.Count == 0)
                return "(no description)";

            return string.Join(' ', tokens);
        }

        private static string ToTitleCaseSafe(string text)
        {
            if (string.Equals(text, "(no description)", StringComparison.OrdinalIgnoreCase))
                return "(no description)";
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text);
        }
    }
}
