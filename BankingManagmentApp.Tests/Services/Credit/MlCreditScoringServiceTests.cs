using System;
using System.IO;
using System.Threading.Tasks;
using BankingManagmentApp.Configuration;
using BankingManagmentApp.Data;
using BankingManagmentApp.Models.ML;
using BankingManagmentApp.Services;
using BankingManagmentApp.Services.Approval; 
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace BankingManagmentApp.Tests.Services
{
    public class MlCreditScoringServiceTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly ApplicationDbContext _db;
        private readonly Mock<IWebHostEnvironment> _env;

        public MlCreditScoringServiceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);

            _env = new Mock<IWebHostEnvironment>();
            _env.SetupGet(e => e.ContentRootPath).Returns(_tempDir);

            var optsDb = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            _db = new ApplicationDbContext(optsDb);
        }

        private MlCreditScoringService CreateService(CreditScoringOptions cfg) =>
            new MlCreditScoringService(_db, _env.Object, Options.Create(cfg));

        [Fact]
        public async Task ComputeAsync_NoUser_ReturnsNull()
        {
            var strictCfg = new CreditScoringOptions
            {
                LookbackDays = 90,
                MinTransactions = 3,
                MinActiveMonths = 1,
                RequireBothFlows = true
            };
            var service = CreateService(strictCfg);

            var result = await service.ComputeAsync("missing-user");

            Assert.Null(result);
        }

        [Fact]
        public async Task ComputeAsync_UserWithFeatures_ReturnsRiskNotes()
        {
            var relaxedCfg = new CreditScoringOptions
            {
                LookbackDays = 90,
                MinTransactions = 0,
                MinActiveMonths = 0,
                RequireBothFlows = false
            };
            var service = CreateService(relaxedCfg);

            _db.Set<CreditFeatures>().Add(new CreditFeatures
            {
                UserId = "u1",
                NumLoans = 0,
                NumAccounts = 2,
                OnTimeRatio = 0.9,
                OverdueRatio = 0.0,
                TotalBalance = 5000m,
                AvgMonthlyInflow = 2000m,
                AvgMonthlyOutflow = 1000m
            });
            await _db.SaveChangesAsync();

            var result = await service.ComputeAsync("u1");

            Assert.NotNull(result);
            Assert.InRange(result!.Score, 300, 850);
            Assert.False(string.IsNullOrWhiteSpace(result.Notes));
        }

        [Fact]
        public async Task ComputeAsync_WithApplication_AdjustsScore()
        {
            var relaxedCfg = new CreditScoringOptions
            {
                LookbackDays = 90,
                MinTransactions = 0,
                MinActiveMonths = 0,
                RequireBothFlows = false
            };
            var service = CreateService(relaxedCfg);

            _db.Set<CreditFeatures>().Add(new CreditFeatures
            {
                UserId = "u2",
                NumLoans = 1,
                NumAccounts = 1,
                OnTimeRatio = 0.8,
                OverdueRatio = 0.1,
                TotalBalance = 3000m,
                AvgMonthlyInflow = 2500m,
                AvgMonthlyOutflow = 2000m
            });
            await _db.SaveChangesAsync();

            var app = new ApplicationFeatures
            {
                RequestedAmount = 12000m,
                TermMonths = 36,
                Product = ProductType.Auto
            };

            var result = await service.ComputeAsync("u2", app);

            Assert.NotNull(result);
            Assert.InRange(result!.Score, 300, 850);
            Assert.Contains("Adj=", result.Notes);
        }

        [Fact]
        public async Task TrainIfNeededAsync_TrainsModel_WhenNoArtifacts()
        {
            var relaxedCfg = new CreditScoringOptions
            {
                LookbackDays = 90,
                MinTransactions = 0,
                MinActiveMonths = 0,
                RequireBothFlows = false
            };
            var service = CreateService(relaxedCfg);

            _db.Set<CreditFeatures>().Add(new CreditFeatures
            {
                UserId = "u3",
                NumLoans = 1,
                NumAccounts = 2,
                OnTimeRatio = 0.9,
                OverdueRatio = 0.05,
                TotalBalance = 10000m,
                AvgMonthlyInflow = 3000m,
                AvgMonthlyOutflow = 1000m
            });
            await _db.SaveChangesAsync();

            await service.TrainIfNeededAsync();

            Assert.True(File.Exists(Path.Combine(_tempDir, "App_Data", "ML", "credit_kmeans.zip")));
            Assert.True(File.Exists(Path.Combine(_tempDir, "App_Data", "ML", "cluster_map.json")));
        }

        [Fact]
        public async Task ForceTrainAsync_AlwaysRetrains_WhenDataExists()
        {
            var relaxedCfg = new CreditScoringOptions
            {
                LookbackDays = 90,
                MinTransactions = 0,
                MinActiveMonths = 0,
                RequireBothFlows = false
            };
            var service = CreateService(relaxedCfg);

            _db.Set<CreditFeatures>().Add(new CreditFeatures
            {
                UserId = "uForce",
                NumLoans = 1,
                NumAccounts = 2,
                OnTimeRatio = 0.95,
                OverdueRatio = 0.05,
                TotalBalance = 8000m,
                AvgMonthlyInflow = 3000m,
                AvgMonthlyOutflow = 1200m
            });
            await _db.SaveChangesAsync();

            await service.ForceTrainAsync();

            var mlDir = Path.Combine(_tempDir, "App_Data", "ML");
            Assert.True(File.Exists(Path.Combine(mlDir, "credit_kmeans.zip")));
            Assert.True(File.Exists(Path.Combine(mlDir, "cluster_map.json")));
        }

        public void Dispose()
        {
            _db.Dispose();
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
    }
}
