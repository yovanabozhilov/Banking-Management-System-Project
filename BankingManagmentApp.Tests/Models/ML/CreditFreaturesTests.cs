using System;
using System.Linq;
using System.Threading.Tasks;
using BankingManagmentApp.Data;
using BankingManagmentApp.Models;
using BankingManagmentApp.Models.ML;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BankingManagmentApp.Tests.Models.ML
{
    public sealed class CreditFeaturesTests : IAsyncLifetime
    {
        private bool _useSqlServer;
        private string _serverCstr = default!;
        private string _serverCstrMaster = default!;
        private string _dbName = default!;
        private string _dbCstr = default!;
        private DbContextOptions<ApplicationDbContext> _options = default!;

        public async Task InitializeAsync()
        {
            var cstrEnv = Environment.GetEnvironmentVariable("TEST_SQLSERVER_CSTR");
            if (!string.IsNullOrWhiteSpace(cstrEnv))
            {
                var builder = new SqlConnectionStringBuilder(cstrEnv);
                var baseCstr = builder.ToString();

                _dbName = $"test_cf_{Guid.NewGuid():N}";

                var masterBuilder = new SqlConnectionStringBuilder(baseCstr) { InitialCatalog = "master" };
                _serverCstrMaster = masterBuilder.ToString();

                var noDbBuilder = new SqlConnectionStringBuilder(baseCstr) { InitialCatalog = "" };
                _serverCstr = noDbBuilder.ToString();

                var dbBuilder = new SqlConnectionStringBuilder(baseCstr) { InitialCatalog = _dbName };
                _dbCstr = dbBuilder.ToString();
            }
            else
            {
                var host = Environment.GetEnvironmentVariable("TEST_SQLSERVER_HOST") ?? "localhost";
                var port = Environment.GetEnvironmentVariable("TEST_SQLSERVER_PORT") ?? "";
                var user = Environment.GetEnvironmentVariable("TEST_SQLSERVER_USER") ?? "";
                var pass = Environment.GetEnvironmentVariable("TEST_SQLSERVER_PASS") ?? "";

                var baseCstr = $"Server={host},{port};User Id={user};Password={pass};TrustServerCertificate=True;Connect Timeout=2;";
                _dbName = $"test_cf_{Guid.NewGuid():N}";

                _serverCstr = baseCstr; 
                _serverCstrMaster = baseCstr + "Database=master;";
                _dbCstr = baseCstr + $"Database={_dbName};";
            }

            _useSqlServer = await CanConnectAsync(_serverCstrMaster);
        }

        public async Task DisposeAsync()
        {
            if (!_useSqlServer) return;
            try
            {
                using var conn = new SqlConnection(_serverCstrMaster);
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"IF DB_ID(@n) IS NOT NULL BEGIN ALTER DATABASE [{_dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_dbName}]; END";
                cmd.Parameters.AddWithValue("@n", _dbName);
                await cmd.ExecuteNonQueryAsync();
            }
            catch { }
        }

        [Fact]
        public async Task Aggregates_PerUser_Are_Calculated_Correctly()
        {
            if (_useSqlServer)
                await RunAgainstSqlServerAsync();
            else
                await RunInMemoryAsync();
        }

        private async Task RunAgainstSqlServerAsync()
        {
            using (var conn = new SqlConnection(_serverCstrMaster))
            {
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"CREATE DATABASE [{_dbName}];";
                await cmd.ExecuteNonQueryAsync();
            }

            using (var ctx = await NewSqlServerContextAsync())
            {
                await ctx.Database.EnsureCreatedAsync();
            }

            var createViewSql = @"
CREATE OR ALTER VIEW dbo.vw_CreditFeatures AS
WITH Monthly AS (
    SELECT a.CustomerId AS UserId,
           YEAR(CONVERT(date, t.[Date]))  AS [Year],
           MONTH(CONVERT(date, t.[Date])) AS [Month],
           SUM(CASE WHEN t.TransactionType = 'Credit' THEN t.Amount ELSE 0 END) AS Inflow,
           SUM(CASE WHEN t.TransactionType = 'Debit'  THEN t.Amount ELSE 0 END) AS Outflow
    FROM Transactions t
    JOIN Accounts a ON a.Id = t.AccountsId
    GROUP BY a.CustomerId, YEAR(CONVERT(date, t.[Date])), MONTH(CONVERT(date, t.[Date]))
),
TxAgg AS (
    SELECT UserId,
           AVG(CAST(Inflow  AS DECIMAL(18,2))) AS AvgMonthlyInflow,
           AVG(CAST(Outflow AS DECIMAL(18,2))) AS AvgMonthlyOutflow
    FROM Monthly
    GROUP BY UserId
),
AccAgg AS (
    SELECT CustomerId AS UserId,
           SUM(Balance) AS TotalBalance,
           COUNT(*)     AS NumAccounts
    FROM Accounts
    GROUP BY CustomerId
),
LoanAgg AS (
    SELECT CustomerId AS UserId,
           COUNT(*) AS NumLoans,
           AVG(CAST(DATEDIFF(DAY, CAST([Date] AS datetime), SYSUTCDATETIME()) AS FLOAT)) AS LoanAgeDaysAvg
    FROM Loans
    GROUP BY CustomerId
),
RepAgg AS (
    SELECT l.CustomerId AS UserId,
           SUM(CASE WHEN r.Status = 'Paid'    THEN 1 ELSE 0 END) AS PaidCount,
           SUM(CASE WHEN r.Status = 'Overdue' THEN 1 ELSE 0 END) AS OverdueCount,
           COUNT(*) AS TotalCount
    FROM LoanRepayments r
    JOIN Loans l ON l.Id = r.LoanId
    GROUP BY l.CustomerId
),
Users AS (
    SELECT CustomerId AS UserId FROM Accounts
    UNION
    SELECT CustomerId FROM Loans
)
SELECT
    u.UserId,
    ISNULL(a.TotalBalance, 0) AS TotalBalance,
    ISNULL(a.NumAccounts, 0)  AS NumAccounts,
    ISNULL(l.NumLoans, 0)     AS NumLoans,
    l.LoanAgeDaysAvg,
    CASE WHEN r.TotalCount > 0 THEN CAST(r.PaidCount    AS FLOAT) / NULLIF(r.TotalCount, 0) ELSE NULL END AS OnTimeRatio,
    CASE WHEN r.TotalCount > 0 THEN CAST(r.OverdueCount AS FLOAT) / NULLIF(r.TotalCount, 0) ELSE NULL END AS OverdueRatio,
    ISNULL(t.AvgMonthlyInflow,  0) AS AvgMonthlyInflow,
    ISNULL(t.AvgMonthlyOutflow, 0) AS AvgMonthlyOutflow
FROM Users u
LEFT JOIN AccAgg a ON a.UserId = u.UserId
LEFT JOIN LoanAgg l ON l.UserId = u.UserId
LEFT JOIN RepAgg r ON r.UserId = u.UserId
LEFT JOIN TxAgg  t ON t.UserId = u.UserId;
";
            using (var ctx = await NewSqlServerContextAsync())
            {
                await ctx.Database.ExecuteSqlRawAsync(createViewSql);
            }

            int acc1Id, acc2Id, loan1Id, loan2Id;
            using (var ctx = await NewSqlServerContextAsync())
            {
                var user = new Customers { Id = "u1", Email = "u1@x.com", UserName = "u1" };
                ctx.Customers.Add(user);
                await ctx.SaveChangesAsync();

                var a1 = new Accounts { CustomerId = "u1", IBAN = "BG00", AccountType = "User", Balance = 100m, Currency = "BGN", CreateAt = DateTime.UtcNow, Status = "Active" };
                var a2 = new Accounts { CustomerId = "u1", IBAN = "BG01", AccountType = "User", Balance =  50m, Currency = "BGN", CreateAt = DateTime.UtcNow, Status = "Active" };
                ctx.Accounts.AddRange(a1, a2);
                await ctx.SaveChangesAsync();
                acc1Id = a1.Id; acc2Id = a2.Id;

                var l1 = new Loans { CustomerId = "u1", Type = "Personal", Amount = 1000m, Date = DateTime.UtcNow.AddDays(-20), Status = "Approved" };
                var l2 = new Loans { CustomerId = "u1", Type = "Personal", Amount =  500m, Date = DateTime.UtcNow.AddDays(-10), Status = "Approved" };
                ctx.Loans.AddRange(l1, l2);
                await ctx.SaveChangesAsync();
                loan1Id = l1.Id; loan2Id = l2.Id;

                var today = DateOnly.FromDateTime(DateTime.Today);
                ctx.LoanRepayments.AddRange(
                    new LoanRepayments { LoanId = loan1Id, DueDate = today, AmountDue = 100, AmountPaid = 100, Status = "Paid" },
                    new LoanRepayments { LoanId = loan1Id, DueDate = today, AmountDue = 100, AmountPaid = 100, Status = "Paid" },
                    new LoanRepayments { LoanId = loan2Id, DueDate = today, AmountDue = 100, AmountPaid = 100, Status = "Paid" },
                    new LoanRepayments { LoanId = loan2Id, DueDate = today, AmountDue = 100, AmountPaid =   0, Status = "Overdue" }
                );

                ctx.Transactions.AddRange(
                    new Transactions { AccountsId = acc1Id, TransactionType = "Credit", Amount = 1000m, Date = DateOnly.FromDateTime(new DateTime(2024, 1, 15)) },
                    new Transactions { AccountsId = acc1Id, TransactionType = "Debit",  Amount =  300m, Date = DateOnly.FromDateTime(new DateTime(2024, 1, 20)) },
                    new Transactions { AccountsId = acc2Id, TransactionType = "Credit", Amount =  500m, Date = DateOnly.FromDateTime(new DateTime(2024, 2, 10)) },
                    new Transactions { AccountsId = acc2Id, TransactionType = "Debit",  Amount =  100m, Date = DateOnly.FromDateTime(new DateTime(2024, 2, 12)) }
                );

                await ctx.SaveChangesAsync();
            }

            using (var ctx = await NewSqlServerContextAsync())
            {
                var row = await ctx.Set<CreditFeatures>().SingleAsync(x => x.UserId == "u1");

                row.UserId.Should().Be("u1");
                row.TotalBalance.Should().Be(150m);
                row.NumAccounts.Should().Be(2);
                row.NumLoans.Should().Be(2);

                row.LoanAgeDaysAvg.Should().NotBeNull();
                row.OnTimeRatio.Should().NotBeNull();
                row.OverdueRatio.Should().NotBeNull();
                row.OnTimeRatio!.Value.Should().BeApproximately(0.75, 1e-6);
                row.OverdueRatio!.Value.Should().BeApproximately(0.25, 1e-6);

                row.AvgMonthlyInflow.Should().Be(750m);
                row.AvgMonthlyOutflow.Should().Be(200m);
            }
        }

        private async Task RunInMemoryAsync()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"cf_mem_{Guid.NewGuid():N}")
                .EnableSensitiveDataLogging()
                .Options;

            using var ctx = new ApplicationDbContext(options);
            await ctx.Database.EnsureCreatedAsync();

            var user = new Customers { Id = "u1", Email = "u1@x.com", UserName = "u1" };
            ctx.Customers.Add(user);
            await ctx.SaveChangesAsync();

            var a1 = new Accounts { CustomerId = "u1", IBAN = "BG00", AccountType = "User", Balance = 100m, Currency = "BGN", CreateAt = DateTime.UtcNow, Status = "Active" };
            var a2 = new Accounts { CustomerId = "u1", IBAN = "BG01", AccountType = "User", Balance =  50m, Currency = "BGN", CreateAt = DateTime.UtcNow, Status = "Active" };
            ctx.Accounts.AddRange(a1, a2);
            await ctx.SaveChangesAsync();

            var l1 = new Loans { CustomerId = "u1", Type = "Personal", Amount = 1000m, Date = DateTime.UtcNow.AddDays(-20), Status = "Approved" };
            var l2 = new Loans { CustomerId = "u1", Type = "Personal", Amount =  500m, Date = DateTime.UtcNow.AddDays(-10), Status = "Approved" };
            ctx.Loans.AddRange(l1, l2);
            await ctx.SaveChangesAsync();

            var today = DateOnly.FromDateTime(DateTime.Today);
            ctx.LoanRepayments.AddRange(
                new LoanRepayments { LoanId = l1.Id, DueDate = today, AmountDue = 100, AmountPaid = 100, Status = "Paid" },
                new LoanRepayments { LoanId = l1.Id, DueDate = today, AmountDue = 100, AmountPaid = 100, Status = "Paid" },
                new LoanRepayments { LoanId = l2.Id, DueDate = today, AmountDue = 100, AmountPaid = 100, Status = "Paid" },
                new LoanRepayments { LoanId = l2.Id, DueDate = today, AmountDue = 100, AmountPaid =   0, Status = "Overdue" }
            );

            ctx.Transactions.AddRange(
                new Transactions { AccountsId = a1.Id, TransactionType = "Credit", Amount = 1000m, Date = DateOnly.FromDateTime(new DateTime(2024, 1, 15)) },
                new Transactions { AccountsId = a1.Id, TransactionType = "Debit",  Amount =  300m, Date = DateOnly.FromDateTime(new DateTime(2024, 1, 20)) },
                new Transactions { AccountsId = a2.Id, TransactionType = "Credit", Amount =  500m, Date = DateOnly.FromDateTime(new DateTime(2024, 2, 10)) },
                new Transactions { AccountsId = a2.Id, TransactionType = "Debit",  Amount =  100m, Date = DateOnly.FromDateTime(new DateTime(2024, 2, 12)) }
            );
            await ctx.SaveChangesAsync();

            var totalBalance = ctx.Accounts.Where(x => x.CustomerId == "u1").Sum(x => x.Balance);
            var numAccounts  = ctx.Accounts.Count(x => x.CustomerId == "u1");
            var loans        = ctx.Loans.Where(x => x.CustomerId == "u1").ToList();
            var numLoans     = loans.Count;
            double? loanAgeDaysAvg = numLoans == 0 ? null : loans.Average(l => (DateTime.UtcNow - l.Date).TotalDays);

            var reps = from r in ctx.LoanRepayments
                       join l in ctx.Loans on r.LoanId equals l.Id
                       where l.CustomerId == "u1"
                       select r;
            var totalCount   = reps.Count();
            double? onTime   = totalCount > 0 ? (double)reps.Count(r => r.Status == "Paid")    / totalCount : null;
            double? overdue  = totalCount > 0 ? (double)reps.Count(r => r.Status == "Overdue") / totalCount : null;

            var monthly = from t in ctx.Transactions
                          join a in ctx.Accounts on t.AccountsId equals a.Id
                          where a.CustomerId == "u1"
                          group t by new { t.Date.Year, t.Date.Month } into g
                          select new
                          {
                              In  = g.Where(x => x.TransactionType == "Credit").Sum(x => x.Amount),
                              Out = g.Where(x => x.TransactionType == "Debit").Sum(x => x.Amount)
                          };
            var months = monthly.ToList();
            var avgIn  = months.Count == 0 ? 0m : months.Average(m => m.In);
            var avgOut = months.Count == 0 ? 0m : months.Average(m => m.Out);

            ctx.CreditFeaturesView.Add(new CreditFeatures
            {
                UserId = "u1",
                TotalBalance = totalBalance,
                NumAccounts = numAccounts,
                NumLoans = numLoans,
                LoanAgeDaysAvg = loanAgeDaysAvg,
                OnTimeRatio = onTime,
                OverdueRatio = overdue,
                AvgMonthlyInflow = avgIn,
                AvgMonthlyOutflow = avgOut
            });
            await ctx.SaveChangesAsync();

            var row = await ctx.CreditFeaturesView.SingleAsync(x => x.UserId == "u1");
            row.TotalBalance.Should().Be(150m);
            row.NumAccounts.Should().Be(2);
            row.NumLoans.Should().Be(2);
            row.LoanAgeDaysAvg.Should().NotBeNull();
            row.OnTimeRatio.Should().NotBeNull();
            row.OverdueRatio.Should().NotBeNull();
            row.OnTimeRatio!.Value.Should().BeApproximately(0.75, 1e-6);
            row.OverdueRatio!.Value.Should().BeApproximately(0.25, 1e-6);
            row.AvgMonthlyInflow.Should().Be(750m);
            row.AvgMonthlyOutflow.Should().Be(200m);
        }

        private async Task<bool> CanConnectAsync(string cstr)
        {
            try
            {
                using var c = new SqlConnection(cstr);
                await c.OpenAsync();
                return true;
            }
            catch { return false; }
        }

        private async Task<ApplicationDbContext> NewSqlServerContextAsync()
        {
            if (_options == null!)
            {
                _options = new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseSqlServer(_dbCstr)
                    .EnableSensitiveDataLogging()
                    .Options;
            }
            var ctx = new ApplicationDbContext(_options);
            await Task.Yield();
            return ctx;
        }
    }
}
