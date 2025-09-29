using Microsoft.AspNetCore.Hosting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BankingManagmentApp.Data;
using BankingManagmentApp.Models.ML;
using BankingManagmentApp.Services.Approval;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;
using Microsoft.Extensions.Options;
using BankingManagmentApp.Configuration;

namespace BankingManagmentApp.Services
{
    public class CreditScoreResult
    {
        public int    Score     { get; set; }
        public int    RiskLevel { get; set; }
        public string Notes     { get; set; } = string.Empty;
    }

    public interface ICreditScoringService
    {
        Task<CreditScoreResult?> ComputeAsync(string userId);
        Task<CreditScoreResult?> ComputeAsync(string userId, ApplicationFeatures app);
        Task TrainIfNeededAsync(CancellationToken ct = default);
        Task ForceTrainAsync(CancellationToken ct = default);
    }

    internal class MlRow
    {
        public string UserId { get; set; } = string.Empty;
        public float TotalBalance      { get; set; }
        public float NumAccounts       { get; set; }
        public float NumLoans          { get; set; }
        public float LoanAgeDaysAvg    { get; set; }
        public float OnTimeRatio       { get; set; }
        public float OverdueRatio      { get; set; }
        public float AvgMonthlyInflow  { get; set; }
        public float AvgMonthlyOutflow { get; set; }
    }

    internal class MlPrediction
    {
        [ColumnName("PredictedLabel")]
        public uint PredictedClusterId { get; set; }
        public float[]? Score { get; set; }
    }

    public class MlCreditScoringService : ICreditScoringService
    {
        private readonly ApplicationDbContext _db;
        private readonly MLContext _ml;

        private readonly string _modelDir;
        private readonly string _modelPath;
        private readonly string _clusterMapPath;

        private ITransformer? _model;
        private Dictionary<int, int>? _clusterToRisk;
        private readonly SemaphoreSlim _lock = new(1, 1);

        private const int MaxRiskLevels = 4;

        private readonly CreditScoringOptions _opts;

        public MlCreditScoringService(
            ApplicationDbContext db,
            IWebHostEnvironment env,
            IOptions<CreditScoringOptions> opts
        )
        {
            _db = db;
            _ml = new MLContext(seed: 42);
            _opts = opts?.Value ?? new CreditScoringOptions();

            _modelDir = Path.Combine(env.ContentRootPath, "App_Data", "ML");
            Directory.CreateDirectory(_modelDir);

            _modelPath      = Path.Combine(_modelDir, "credit_kmeans.zip");
            _clusterMapPath = Path.Combine(_modelDir, "cluster_map.json");
        }

        private class LiveSnapshot
        {
            public decimal TotalBalance { get; set; }
            public int     NumAccounts  { get; set; }
            public int     NumLoans     { get; set; }
            public double  LoanAgeDaysAvg { get; set; }
            public double  OnTimeRatio    { get; set; }
            public double  OverdueRatio   { get; set; }
            public decimal AvgMonthlyInflow  { get; set; }
            public decimal AvgMonthlyOutflow { get; set; }
        }

        private static string ComposeUserNote(LiveSnapshot s)
        {
            var inv = CultureInfo.InvariantCulture;
            var net = s.AvgMonthlyInflow - s.AvgMonthlyOutflow;

            var lines = new List<string>();

            if (s.NumLoans > 0)
            {
                lines.Add($"Loans: {s.NumLoans}");
                lines.Add($"    • On-time: {(s.OnTimeRatio).ToString("P0", inv)}");
                lines.Add($"    • Overdue: {(s.OverdueRatio).ToString("P0", inv)}");
            }
            else
            {
                lines.Add("Loans: 0");
            }

            lines.Add($"Accounts: {s.NumAccounts}");
            lines.Add($"Total balance: {s.TotalBalance.ToString("0.00", inv)}");
            lines.Add($"Average monthly net: {net.ToString("+0.00;-0.00;0.00", inv)}");

            return string.Join("\n", lines);
        }

        public async Task<CreditScoreResult?> ComputeAsync(string userId)
        {
            await TrainIfNeededAsync();

            if (!await HasAnyUserDataAsync(userId))
                return null;

            var snap = await GetLiveSnapshotAsync(userId, _opts.LookbackDays);

            var row = new MlRow
            {
                UserId            = userId,
                TotalBalance      = (float)snap.TotalBalance,
                NumAccounts       = snap.NumAccounts,
                NumLoans          = snap.NumLoans,
                LoanAgeDaysAvg    = (float)snap.LoanAgeDaysAvg,
                OnTimeRatio       = (float)snap.OnTimeRatio,
                OverdueRatio      = (float)snap.OverdueRatio,
                AvgMonthlyInflow  = (float)snap.AvgMonthlyInflow,
                AvgMonthlyOutflow = (float)snap.AvgMonthlyOutflow
            };
            Sanitize(row);

            if (_model is null || _clusterToRisk is null || _clusterToRisk.Count == 0)
                return HeuristicFallback(row, snap);

            var engine  = _ml.Model.CreatePredictionEngine<MlRow, MlPrediction>(_model);
            var pred    = engine.Predict(row);
            var cluster = (int)pred.PredictedClusterId;

            var initialRisk = _clusterToRisk.TryGetValue(cluster, out var r) ? r : MaxRiskLevels;
            var score = ScoreFromRiskAndFeatures(initialRisk, row);
            
            int risk = score >= 720 ? 1 :
                       score >= 660 ? 2 :
                       score >= 600 ? 3 : 4;
            
            var notes = ComposeUserNote(snap);
            
            return new CreditScoreResult
            {
                Score     = score,
                RiskLevel = risk,
                Notes     = notes
            };
        }

        public async Task<CreditScoreResult?> ComputeAsync(string userId, ApplicationFeatures app)
        {
            var baseRes = await ComputeAsync(userId);
            if (baseRes is null) return null;

            var snap = await GetLiveSnapshotAsync(userId, _opts.LookbackDays);
            decimal netFlow = snap.AvgMonthlyInflow - snap.AvgMonthlyOutflow;
            if (netFlow < 0) netFlow = 0;

            decimal req = app.RequestedAmount <= 0 ? 1m : app.RequestedAmount;
            decimal affordability = netFlow == 0 ? 0m : (netFlow * 0.40m) / req;

            int scoreAdj = 0;

            if (req > 20000) scoreAdj -= 30;
            else if (req > 10000) scoreAdj -= 15;
            else if (req > 5000) scoreAdj -= 5;

            if (affordability >= 1.0m) scoreAdj += 15;
            else if (affordability >= 0.7m) scoreAdj += 5;
            else if (affordability < 0.4m) scoreAdj -= 15;

            if (app.TermMonths >= 36) scoreAdj -= 10;
            else if (app.TermMonths >= 24) scoreAdj -= 5;

            switch (app.Product)
            {
                case ProductType.Mortgage:   scoreAdj += 5;  break;
                case ProductType.Personal:   scoreAdj += 0;  break;
                case ProductType.Auto:       scoreAdj += 2;  break;
                case ProductType.CreditCard: scoreAdj -= 10; break;
            }

            var finalScore = Math.Clamp(baseRes.Score + scoreAdj, 300, 850);
            int risk = finalScore >= 720 ? 1 :
                       finalScore >= 660 ? 2 :
                       finalScore >= 600 ? 3 : 4;

            return new CreditScoreResult
            {
                Score     = finalScore,
                RiskLevel = risk,
                Notes     = $"Base={baseRes.Score}, Adj={scoreAdj}, Afford={affordability:F2}"
            };
        }

        public async Task TrainIfNeededAsync(CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                if (_model is not null && _clusterToRisk is not null && _clusterToRisk.Count > 0)
                    return;

                if (File.Exists(_modelPath))
                {
                    using var fs = File.OpenRead(_modelPath);
                    _model = _ml.Model.Load(fs, out _);
                }

                if (File.Exists(_clusterMapPath))
                {
                    var json = await File.ReadAllTextAsync(_clusterMapPath, ct);
                    _clusterToRisk = JsonSerializer.Deserialize<Dictionary<int, int>>(json);
                }

                if (_model is null || _clusterToRisk is null || _clusterToRisk.Count == 0)
                {
                    await TrainInternalAsync(ct);
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task ForceTrainAsync(CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                await TrainInternalAsync(ct);
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task TrainInternalAsync(CancellationToken ct)
        {
            var feats = await _db.Set<CreditFeatures>()
                .AsNoTracking()
                .ToListAsync(ct);

            var rows = feats.Select(ToMlRow).ToList();
            rows.ForEach(Sanitize);

            var n = rows.Count;
            if (n == 0)
            {
                _model = null;
                _clusterToRisk = null;
                return;
            }

            var k = Math.Min(MaxRiskLevels, Math.Max(1, n));

            var dv = _ml.Data.LoadFromEnumerable(rows);

            var featureCols = new[]
            {
                nameof(MlRow.TotalBalance),
                nameof(MlRow.NumAccounts),
                nameof(MlRow.NumLoans),
                nameof(MlRow.LoanAgeDaysAvg),
                nameof(MlRow.OnTimeRatio),
                nameof(MlRow.OverdueRatio),
                nameof(MlRow.AvgMonthlyInflow),
                nameof(MlRow.AvgMonthlyOutflow)
            };

            var pipeline =
                _ml.Transforms.Concatenate("Features", featureCols)
                  .Append(_ml.Transforms.NormalizeMinMax("Features"))
                  .Append(_ml.Clustering.Trainers.KMeans(new KMeansTrainer.Options
                    {
                        FeatureColumnName = "Features",
                        NumberOfClusters = k,
                        MaximumNumberOfIterations = 200
                    }));

            var model = pipeline.Fit(dv);

            var transformed = model.Transform(dv);
            var preds = _ml.Data.CreateEnumerable<MlPrediction>(transformed, reuseRowObject: false).ToList();

            var joined = rows.Zip(preds, (r, p) => new
            {
                Cluster = (int)p.PredictedClusterId,
                r.OnTimeRatio,
                r.OverdueRatio,
                r.TotalBalance
            });

            var stats = joined
                .GroupBy(x => x.Cluster)
                .Select(g => new
                {
                    Cluster = g.Key,
                    OverdueAvg = g.Average(z => z.OverdueRatio),
                    OnTimeAvg  = g.Average(z => z.OnTimeRatio),
                    BalanceAvg = g.Average(z => z.TotalBalance)
                })
                .OrderBy(s => s.OverdueAvg)
                .ThenByDescending(s => s.OnTimeAvg)
                .ToList();

            var mapping = new Dictionary<int, int>();
            for (int i = 0; i < stats.Count; i++)
            {
                var clusterId = stats[i].Cluster;
                var riskLevel = Math.Min(MaxRiskLevels, i + 1);
                mapping[clusterId] = riskLevel;
            }

            Directory.CreateDirectory(_modelDir);

            using (var fs = File.Create(_modelPath))
                _ml.Model.Save(model, dv.Schema, fs);

            var json = JsonSerializer.Serialize(mapping, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_clusterMapPath, json, ct);

            _model = model;
            _clusterToRisk = mapping;
        }

        private async Task<LiveSnapshot> GetLiveSnapshotAsync(string userId, int lookbackDays)
        {
            var accountsQ = _db.Accounts.Where(a => a.CustomerId == userId);
            var totalBalance = await accountsQ.SumAsync(a => (decimal?)a.Balance) ?? 0m;
            var numAccounts  = await accountsQ.CountAsync();

            var loans = await _db.Loans.Where(l => l.CustomerId == userId).ToListAsync();
            var numLoans = loans.Count;
            double loanAgeDaysAvg = numLoans > 0
                ? loans.Average(l => (DateTime.UtcNow - l.Date).TotalDays)
                : 0.0;

            double onTimeRatio = 0.0, overdueRatio = 0.0;
            if (numLoans > 0)
            {
                var loanIds = loans.Select(l => l.Id).ToList();
                var reps = await _db.LoanRepayments
                    .Where(r => loanIds.Contains(r.LoanId))
                    .ToListAsync();

                var total = reps.Count;
                if (total > 0)
                {
                    bool Is(string? s, string target) =>
                        !string.IsNullOrEmpty(s) &&
                        s.Equals(target, StringComparison.OrdinalIgnoreCase);

                    var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
                    var onTime   = reps.Count(r => Is(r.Status, "Paid"));
                    var overdue1 = reps.Count(r => Is(r.Status, "Overdue"));
                    var overdue2 = reps.Count(r => r.DueDate < today && !Is(r.Status, "Paid"));
                    var overdue = Math.Max(overdue1, overdue2);

                    onTimeRatio  = (double)onTime  / total;
                    overdueRatio = (double)overdue / total;
                }
            }

            var since = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-lookbackDays));
            var tx = await _db.Transactions
                .Include(t => t.Accounts)
                .Where(t => t.Accounts != null &&
                            t.Accounts.CustomerId == userId &&
                            t.Date >= since)
                .ToListAsync();

            static bool Has(string? s, string token) =>
                !string.IsNullOrEmpty(s) &&
                s.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;

            decimal inflowTotal = 0m, outflowTotal = 0m;
            foreach (var t in tx)
            {
                var type = t.TransactionType ?? "";
                if (Has(type, "debit") || Has(type, "repay") || Has(type, "payment"))
                    outflowTotal += t.Amount;
                else if (Has(type, "credit") || Has(type, "disbursement"))
                    inflowTotal += t.Amount;
            }

            var months = Math.Max(1m, (decimal)lookbackDays / 30m);
            var avgIn  = inflowTotal / months;
            var avgOut = outflowTotal / months;

            return new LiveSnapshot
            {
                TotalBalance      = totalBalance,
                NumAccounts       = numAccounts,
                NumLoans          = numLoans,
                LoanAgeDaysAvg    = loanAgeDaysAvg,
                OnTimeRatio       = onTimeRatio,
                OverdueRatio      = overdueRatio,
                AvgMonthlyInflow  = avgIn,
                AvgMonthlyOutflow = avgOut
            };
        }

        private static MlRow ToMlRow(CreditFeatures f) => new()
        {
            UserId           = f.UserId,
            TotalBalance     = (float)f.TotalBalance,
            NumAccounts      = f.NumAccounts,
            NumLoans         = f.NumLoans,
            LoanAgeDaysAvg   = (float)(f.LoanAgeDaysAvg ?? 0.0),
            OnTimeRatio      = (float)(f.OnTimeRatio    ?? 0.0),
            OverdueRatio     = (float)(f.OverdueRatio   ?? 0.0),
            AvgMonthlyInflow = (float)f.AvgMonthlyInflow,
            AvgMonthlyOutflow= (float)f.AvgMonthlyOutflow
        };

        private static void Sanitize(MlRow r)
        {
            if (float.IsNaN(r.OnTimeRatio)    || float.IsInfinity(r.OnTimeRatio))    r.OnTimeRatio    = 0f;
            if (float.IsNaN(r.OverdueRatio)   || float.IsInfinity(r.OverdueRatio))   r.OverdueRatio   = 0f;
            if (float.IsNaN(r.LoanAgeDaysAvg) || float.IsInfinity(r.LoanAgeDaysAvg)) r.LoanAgeDaysAvg = 0f;
        }

        private static int ClampScore(int x) => Math.Max(300, Math.Min(850, x));

        private static int ScoreFromRiskAndFeatures(int risk, MlRow r)
        {
            int @base = risk switch
            {
                1 => 760,
                2 => 690,
                3 => 630,
                _ => 560
            };

            var netFlow = r.AvgMonthlyInflow - r.AvgMonthlyOutflow;

            var delta =
                (int)(
                    60.0f * (r.OnTimeRatio - r.OverdueRatio) +
                    0.0035f * r.TotalBalance +
                    0.02f * netFlow -
                    8.0f * r.NumLoans +
                    2.0f * r.NumAccounts -
                    0.01f * r.LoanAgeDaysAvg
                );

            return ClampScore(@base + delta);
        }

        private static CreditScoreResult HeuristicFallback(MlRow r, LiveSnapshot s)
        {
            var baseScore = 650.0;

            if (r.NumLoans == 0) baseScore += 10;
            baseScore += 60.0 * (r.OnTimeRatio - r.OverdueRatio);
            baseScore += 0.0035 * r.TotalBalance;
            baseScore += 0.02   * (r.AvgMonthlyInflow - r.AvgMonthlyOutflow);
            baseScore -= 8.0    * r.NumLoans;
            baseScore += 2.0    * r.NumAccounts;

            var final = ClampScore((int)Math.Round(baseScore));

            var risk =
                final >= 720 ? 1 :
                final >= 660 ? 2 :
                final >= 600 ? 3 : 4;

            var notes = ComposeUserNote(s);

            return new CreditScoreResult
            {
                Score     = final,
                RiskLevel = risk,
                Notes     = notes
            };
        }

        private record TxStats(int Count, int DistinctMonths, decimal Inflow, decimal Outflow);

        private async Task<TxStats> GetTxStatsAsync(string userId, int lookbackDays)
        {
            var since = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-lookbackDays));

            var tx = await _db.Transactions
                .Include(t => t.Accounts)
                .Where(t => t.Accounts != null &&
                            t.Accounts.CustomerId == userId &&
                            t.Date >= since)
                .Select(t => new { t.Date, t.Amount, t.TransactionType })
                .ToListAsync();

            static bool Has(string? s, string token) =>
                !string.IsNullOrEmpty(s) &&
                s.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;

            decimal inflow = 0m, outflow = 0m;
            foreach (var t in tx)
            {
                var type = t.TransactionType ?? "";
                if (Has(type, "debit") || Has(type, "repay") || Has(type, "payment"))
                    outflow += t.Amount;
                else if (Has(type, "credit") || Has(type, "disbursement"))
                    inflow += t.Amount;
            }

            var months = tx
                .Select(t => new { t.Date.Year, t.Date.Month })
                .Distinct()
                .Count();

            return new TxStats(tx.Count, months, inflow, outflow);
        }

        private async Task<bool> HasAnyUserDataAsync(string userId)
        {
            var lookbackDays  = _opts.LookbackDays <= 0 ? 90 : _opts.LookbackDays;
            var minTx         = Math.Max(0, _opts.MinTransactions);
            var minMonths     = Math.Max(1, _opts.MinActiveMonths);

            if (minTx <= 0) return true;

            var txs = await GetTxStatsAsync(userId, lookbackDays);
            if (txs.Count >= minTx &&
                txs.DistinctMonths >= minMonths &&
                (!_opts.RequireBothFlows || (txs.Inflow > 0 && txs.Outflow > 0)))
            {
                return true;
            }

            return false;
        }
    }
}
