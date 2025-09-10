using Microsoft.AspNetCore.Hosting;
using System;
using System.Collections.Generic;
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

        public MlCreditScoringService(ApplicationDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _ml = new MLContext(seed: 42);

            _modelDir = Path.Combine(env.ContentRootPath, "App_Data", "ML");
            Directory.CreateDirectory(_modelDir);

            _modelPath      = Path.Combine(_modelDir, "credit_kmeans.zip");
            _clusterMapPath = Path.Combine(_modelDir, "cluster_map.json");
        }

        // ---------- Публични методи ----------

        public async Task<CreditScoreResult?> ComputeAsync(string userId)
        {
            await TrainIfNeededAsync();

            var f = await _db.Set<CreditFeatures>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId);

            if (f == null)
                return null; 

            var row = ToMlRow(f);
            
            Sanitize(row);

            if (_model is null || _clusterToRisk is null || _clusterToRisk.Count == 0)
                return HeuristicFallback(row, f);

            var engine = _ml.Model.CreatePredictionEngine<MlRow, MlPrediction>(_model);
            var pred   = engine.Predict(row);
            var cluster = (int)pred.PredictedClusterId;

            var risk = _clusterToRisk.TryGetValue(cluster, out var r)
                ? r
                : MaxRiskLevels; 

            var score = ScoreFromRiskAndFeatures(risk, row);

            var notes =
                $"Cluster: {cluster} → Risk {risk}; " +
                $"Loans: {f.NumLoans}, Accounts: {f.NumAccounts}, " +
                $"OnTime: {f.OnTimeRatio:0.00}, Overdue: {f.OverdueRatio:0.00}, " +
                $"Balance: {f.TotalBalance:0.00}, NetFlow: {(f.AvgMonthlyInflow - f.AvgMonthlyOutflow):0.00}";

            return new CreditScoreResult
            {
                Score = score,
                RiskLevel = risk,
                Notes = notes
            };
        }

        public async Task<CreditScoreResult?> ComputeAsync(string userId, ApplicationFeatures app)
        {
            var baseRes = await ComputeAsync(userId);
            if (baseRes is null) return null;

            var f = await _db.Set<CreditFeatures>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId);

            decimal netFlow = ((f?.AvgMonthlyInflow ?? 0m) - (f?.AvgMonthlyOutflow ?? 0m));
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

        // ---------- Вътрешно: тренировка ----------

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

        // ---------- Помощни ----------

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
            if (float.IsNaN(r.OnTimeRatio)   || float.IsInfinity(r.OnTimeRatio))   r.OnTimeRatio   = 0f;
            if (float.IsNaN(r.OverdueRatio)  || float.IsInfinity(r.OverdueRatio))  r.OverdueRatio  = 0f;
            if (float.IsNaN(r.LoanAgeDaysAvg)|| float.IsInfinity(r.LoanAgeDaysAvg))r.LoanAgeDaysAvg= 0f;
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

        private static CreditScoreResult HeuristicFallback(MlRow r, CreditFeatures f)
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

            var notes =
                $"Heuristic fallback; Loans: {f.NumLoans}, Accounts: {f.NumAccounts}, " +
                $"OnTime: {f.OnTimeRatio:0.00}, Overdue: {f.OverdueRatio:0.00}, " +
                $"Balance: {f.TotalBalance:0.00}";

            return new CreditScoreResult
            {
                Score = final,
                RiskLevel = risk,
                Notes = notes
            };
        }
    }
}
