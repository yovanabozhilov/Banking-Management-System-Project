using BankingManagmentApp.Data;
using BankingManagmentApp.Models;
using BankingManagmentApp.Services.Forecasting;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace BankingManagmentApp.Tests.Services
{
    public class ForecastServiceTests
    {
        private ApplicationDbContext GetDbContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public void ForecastTransactionVolumeMonthly_ReturnsCorrectCounts()
        {
            var context = GetDbContext(nameof(ForecastTransactionVolumeMonthly_ReturnsCorrectCounts));
            context.Transactions.AddRange(new List<Transactions>
            {
                new Transactions { Date = new DateOnly(2025, 9, 1), Amount = 100, TransactionType = "Debit" },
                new Transactions { Date = new DateOnly(2025, 9, 15), Amount = 200, TransactionType = "Credit" },
                new Transactions { Date = new DateOnly(2025, 8, 20), Amount = 300, TransactionType = "Debit" }
            });
            context.SaveChanges();

            var service = new ForecastService(context);

            var result = service.ForecastTransactionVolumeMonthly();

            Assert.Equal(2, result["2025-09"]);
            Assert.Equal(1, result["2025-08"]);
        }

        [Fact]
        public void ForecastAvgTransactionValue_ReturnsAverage()
        {
            var context = GetDbContext(nameof(ForecastAvgTransactionValue_ReturnsAverage));
            context.Transactions.AddRange(
                new Transactions { Date = new DateOnly(2025, 9, 1), Amount = 100, TransactionType = "Debit" },
                new Transactions { Date = new DateOnly(2025, 9, 2), Amount = 200, TransactionType = "Credit" }
            );
            context.SaveChanges();

            var service = new ForecastService(context);

            var avg = service.ForecastAvgTransactionValue();

            Assert.Equal(150, avg);
        }

        [Fact]
        public void DetectTransactionAnomalies_ReturnsAnomalies()
        {
            var context = GetDbContext(nameof(DetectTransactionAnomalies_ReturnsAnomalies));
        
            for (int i = 0; i < 20; i++)
            {
                context.Transactions.Add(new Transactions
                {
                    Date = new DateOnly(2025, 9, 1),
                    Amount = 100,
                    TransactionType = "Debit"
                });
            }
        
            context.Transactions.Add(new Transactions
            {
                Date = new DateOnly(2025, 9, 2),
                Amount = 10000,
                TransactionType = "Credit"
            });
        
            context.SaveChanges();
        
            var service = new ForecastService(context);
        
            var anomalies = service.DetectTransactionAnomalies();
        
            Assert.Single(anomalies);
            Assert.Equal(10000, anomalies.First().Amount);
        }


        [Fact]
        public void ForecastOverdueLoansRate_CalculatesCorrectly()
        {
            var context = GetDbContext(nameof(ForecastOverdueLoansRate_CalculatesCorrectly));
            context.LoanRepayments.AddRange(
                new LoanRepayments { AmountDue = 100, AmountPaid = 50, Status = "Overdue" }, 
                new LoanRepayments { AmountDue = 100, AmountPaid = 100, Status = "Paid" }
            );

            context.SaveChanges();

            var service = new ForecastService(context);

            var result = service.ForecastOverdueLoansRate();

            Assert.Equal(0.5, result);
        }

        [Fact]
        public void ForecastChurnRate_ReturnsInactiveRatio()
        {
            var context = GetDbContext(nameof(ForecastChurnRate_ReturnsInactiveRatio));
            context.Users.AddRange(
                new Customers { UserName = "u1", IsActive = true },
                new Customers { UserName = "u2", IsActive = false },
                new Customers { UserName = "u3", IsActive = false }
            );
            context.SaveChanges();

            var service = new ForecastService(context);

            var churn = service.ForecastChurnRate();

            Assert.Equal(2d / 3d, churn, 3); 
        }
    }
}
