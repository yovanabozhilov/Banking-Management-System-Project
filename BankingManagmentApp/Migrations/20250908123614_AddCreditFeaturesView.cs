using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankingManagmentApp.Migrations
{
    public partial class AddCreditFeaturesView : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DROP във отделен batch
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.vw_CreditFeatures', N'V') IS NOT NULL
    DROP VIEW dbo.vw_CreditFeatures;
");

            // CREATE във отделен batch (без GO)
            migrationBuilder.Sql(@"
CREATE VIEW dbo.vw_CreditFeatures
AS
SELECT
    a.CustomerId                                        AS UserId,
    ISNULL(SUM(a.Balance), 0.0)                         AS TotalBalance,
    COUNT(DISTINCT a.Id)                                AS NumAccounts,
    COUNT(DISTINCT l.Id)                                AS NumLoans,
    AVG(CAST(DATEDIFF(DAY, l.[Date], GETDATE()) AS float)) AS LoanAgeDaysAvg,
    CAST(SUM(CASE WHEN r.Status = 'Paid' THEN 1 ELSE 0 END) AS float) / NULLIF(COUNT(r.Id), 0) AS OnTimeRatio,
    CAST(SUM(CASE WHEN r.Status = 'Overdue' THEN 1 ELSE 0 END) AS float) / NULLIF(COUNT(r.Id), 0) AS OverdueRatio,
    (
        SELECT ISNULL(SUM(CASE WHEN t.TransactionType = 'Credit' THEN t.Amount ELSE 0 END), 0.0) / 6.0
        FROM Transactions t
        WHERE t.AccountsId IN (SELECT Id FROM Accounts WHERE CustomerId = a.CustomerId)
          AND t.[Date] >= CAST(DATEADD(MONTH, -6, GETDATE()) AS date)
    ) AS AvgMonthlyInflow,
    (
        SELECT ISNULL(SUM(CASE WHEN t.TransactionType = 'Debit' THEN t.Amount ELSE 0 END), 0.0) / 6.0
        FROM Transactions t
        WHERE t.AccountsId IN (SELECT Id FROM Accounts WHERE CustomerId = a.CustomerId)
          AND t.[Date] >= CAST(DATEADD(MONTH, -6, GETDATE()) AS date)
    ) AS AvgMonthlyOutflow
FROM Accounts a
LEFT JOIN Loans l ON l.CustomerId = a.CustomerId
LEFT JOIN LoanRepayments r ON r.LoanId = l.Id
GROUP BY a.CustomerId;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.vw_CreditFeatures', N'V') IS NOT NULL
    DROP VIEW dbo.vw_CreditFeatures;
");
        }
    }
}
