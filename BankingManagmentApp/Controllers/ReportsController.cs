using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using BankingManagmentApp.Data;
using BankingManagmentApp.ViewModels.Reports;
using BankingManagmentApp.Services.Pdf;
using BankingManagmentApp.Services.Excel;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using BankingManagmentApp.Services;

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
                Rows = []
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

            // Ако идва от AdminDashboard през fetch – връщаме само таблицата
            if (Request.Headers.TryGetValue("X-Requested-With", out var xrw) &&
                string.Equals(xrw, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            {
                return PartialView("_ReportTable", vm);
            }

            // Иначе цялата Reports страница
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
            // You will need to get the recipient's email address, e.g., from the current user.
            var userEmail = User.Identity?.Name;
            var doc = new FinancialReportPdf(vm);
            var bytes = doc.GeneratePdf();
            if (!string.IsNullOrEmpty(userEmail))
            {
                await _emailService.SendEmailWithAttachmentAsync(userEmail, emailSubject, emailBody, bytes, fileName);
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

            // Send the Excel file via email
            var emailSubject = $"Financial Report {fileName}";
            var emailBody = "Please find your requested financial report attached.";
            // Get the recipient's email address
            var userEmail = User.Identity?.Name;
            var bytes = FinancialReportExcel.Build(vm);
            //var fileName = $"FinancialReport_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            if (!string.IsNullOrEmpty(userEmail))
            {
                await _emailService.SendEmailWithAttachmentAsync(userEmail, emailSubject, emailBody, bytes, fileName);
            }
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        private static void NormalizeFilters(ReportFilterVm filters)
        {
            // Попълваме разумни стойности и гарантираме валиден интервал
            var today = DateOnly.FromDateTime(DateTime.Today);
            filters.To ??= today;
            filters.From ??= new DateOnly(today.Year, today.Month, 1).AddMonths(-11);

            if (filters.From > filters.To)
            {
                // разменяме ако са обърнати
                var tmp = filters.From;
                filters.From = filters.To;
                filters.To = tmp;
            }

            if (filters.GroupBy != ReportGroupBy.Monthly && filters.GroupBy != ReportGroupBy.Yearly)
                filters.GroupBy = ReportGroupBy.Monthly;
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
            var q = _db.Transactions.AsNoTracking().AsQueryable();

            if (filters.AccountId.HasValue)
                q = q.Where(t => t.AccountsId == filters.AccountId.Value);

            if (filters.From.HasValue)
                q = q.Where(t => t.Date >= filters.From.Value);

            if (filters.To.HasValue)
                q = q.Where(t => t.Date <= filters.To.Value);

            if (filters.GroupBy == ReportGroupBy.Monthly)
            {
                var rows = await q
                    .GroupBy(t => new { t.Date.Year, t.Date.Month })
                    .Select(g => new ReportRow
                    {
                        Year = g.Key.Year,
                        Month = g.Key.Month,
                        TotalTransactions = g.Count(),
                        TotalAmount = g.Sum(x => x.Amount)
                    })
                    .ToListAsync();

                var typeSums = await q
                    .GroupBy(t => new { t.Date.Year, t.Date.Month, t.TransactionType })
                    .Select(g => new
                    {
                        g.Key.Year,
                        g.Key.Month,
                        g.Key.TransactionType,
                        Total = g.Sum(x => x.Amount)
                    })
                    .ToListAsync();

                foreach (var r in rows)
                {
                    r.AmountByType = typeSums
                        .Where(ts => ts.Year == r.Year && ts.Month == r.Month)
                        .ToDictionary(ts => ts.TransactionType ?? "Unknown", ts => ts.Total);
                }

                return new ReportResultVm
                {
                    Filters = filters,
                    Rows = rows.OrderBy(x => x.Year).ThenBy(x => x.Month ?? 0).ToList()
                };
            }
            else
            {
                var rows = await q
                    .GroupBy(t => new { t.Date.Year })
                    .Select(g => new ReportRow
                    {
                        Year = g.Key.Year,
                        Month = null,
                        TotalTransactions = g.Count(),
                        TotalAmount = g.Sum(x => x.Amount)
                    })
                    .ToListAsync();

                var typeSums = await q
                    .GroupBy(t => new { t.Date.Year, t.TransactionType })
                    .Select(g => new
                    {
                        g.Key.Year,
                        g.Key.TransactionType,
                        Total = g.Sum(x => x.Amount)
                    })
                    .ToListAsync();

                foreach (var r in rows)
                {
                    r.AmountByType = typeSums
                        .Where(ts => ts.Year == r.Year)
                        .ToDictionary(ts => ts.TransactionType ?? "Unknown", ts => ts.Total);
                }

                return new ReportResultVm
                {
                    Filters = filters,
                    Rows = rows.OrderBy(x => x.Year).ToList()
                };
            }
        }
    }
}
