// Services/Credit/MlCreditScoringService.cs
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
using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;

namespace BankingManagmentApp.Services
{
    // DTO като при теб
    public class CreditScoreResult
    {
        public int    Score     { get; set; }
        public int    RiskLevel { get; set; } // 1..4
        public string Notes     { get; set; } = string.Empty;
    }

    public interface ICreditScoringService
    {
        Task<CreditScoreResult?> ComputeAsync(string userId);
        Task TrainIfNeededAsync(CancellationToken ct = default);
        Task ForceTrainAsync(CancellationToken ct = default);
    }

    // Вход за ML (float-и)
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

        // KMeans връща и Score вектор (разстояния), но не ни е нужен тук
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
        private Dictionary<int, int>? _clusterToRisk; // { clusterId(1..k) -> Risk(1..4) }
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

            // 1) Четем фийчърите от изгледа
            var f = await _db.Set<CreditFeatures>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId);

            if (f == null)
                return null; // няма данни → няма скор

            var row = ToMlRow(f);
            // safe guard да няма NaN
            Sanitize(row);

            // 2) Ако няма ML модел/кластер map (edge case), fallback към проста формула
            if (_model is null || _clusterToRisk is null || _clusterToRisk.Count == 0)
                return HeuristicFallback(row, f);

            // 3) Инференс
            var engine = _ml.Model.CreatePredictionEngine<MlRow, MlPrediction>(_model);
            var pred   = engine.Predict(row);
            var cluster = (int)pred.PredictedClusterId;

            // 4) Мапваме към риск
            var risk = _clusterToRisk.TryGetValue(cluster, out var r)
                ? r
                : MaxRiskLevels; // worst

            // 5) Калкулираме скор в [300..850] с вътрешна корекция
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

        public async Task TrainIfNeededAsync(CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                if (_model is not null && _clusterToRisk is not null && _clusterToRisk.Count > 0)
                    return; // вече е заредено

                // опит за зареждане от диск
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

                // Ако не успеем да заредим – тренираме
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
            // 1) Данни от изгледа
            var feats = await _db.Set<CreditFeatures>()
                .AsNoTracking()
                .ToListAsync(ct);

            var rows = feats.Select(ToMlRow).ToList();
            rows.ForEach(Sanitize);

            // нужно е поне 2 наблюдения за смислен KMeans
            var n = rows.Count;
            if (n == 0)
            {
                _model = null;
                _clusterToRisk = null;
                return;
            }

            // 2) K броя клъстери: максимум 4, но не повече от n
            var k = Math.Min(MaxRiskLevels, Math.Max(1, n));

            // 3) Pipeline: concat → normalize → KMeans
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

            // 4) Създаваме map cluster -> risk, сортирайки клъстерите по OverdueRatio (по-малко е по-добър риск)
            var transformed = model.Transform(dv);
            var preds = _ml.Data.CreateEnumerable<MlPrediction>(transformed, reuseRowObject: false).ToList();

            var joined = rows.Zip(preds, (r, p) => new
            {
                Cluster = (int)p.PredictedClusterId, // 1..k
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
                // основен критерий: OverdueAvg; вторичен: OnTimeAvg (по-голямо е по-добре)
                .OrderBy(s => s.OverdueAvg)
                .ThenByDescending(s => s.OnTimeAvg)
                .ToList();

            var mapping = new Dictionary<int, int>(); // cluster -> risk
            for (int i = 0; i < stats.Count; i++)
            {
                var clusterId = stats[i].Cluster;
                var riskLevel = Math.Min(MaxRiskLevels, i + 1); // 1..4 (ако k<4, последните нива просто не се ползват)
                mapping[clusterId] = riskLevel;
            }

            // 5) Записваме на диск
            Directory.CreateDirectory(_modelDir);

            using (var fs = File.Create(_modelPath))
                _ml.Model.Save(model, dv.Schema, fs);

            var json = JsonSerializer.Serialize(mapping, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_clusterMapPath, json, ct);

            // 6) Зареждаме в памет
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

        // Базов скор по риск + вътрешна корекция по фийчърите
        private static int ScoreFromRiskAndFeatures(int risk, MlRow r)
        {
            // Базови средни по риск
            int @base = risk switch
            {
                1 => 760,
                2 => 690,
                3 => 630,
                _ => 560
            };

            // Вътрешна корекция (малка, за стабилност)
            // Идеята е да не „скачат“ много; тежестите са умерени.
            var netFlow = r.AvgMonthlyInflow - r.AvgMonthlyOutflow;

            var delta =
                (int)(
                    60.0f * (r.OnTimeRatio - r.OverdueRatio) +   // платено vs просрочено
                    0.0035f * r.TotalBalance +                  // 350 лв. на 100k
                    0.02f * netFlow -                           // нетен мес. поток
                    8.0f * r.NumLoans +                         // повече кредити → по-ниско
                    2.0f * r.NumAccounts -                      // повече акаунти → лек плюс
                    0.01f * r.LoanAgeDaysAvg                    // много стари кредити → лек минус
                );

            return ClampScore(@base + delta);
        }

        // Ако нямаме модел/данни – ползваме скромна формула (твоята идея, леко рационализирана)
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
