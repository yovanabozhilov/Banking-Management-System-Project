using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace BankingManagmentApp.Services
{
    public class MlRetrainHostedService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<MlRetrainHostedService> _logger;
        private readonly TimeSpan _dailyTime = new(3, 15, 0);
        private readonly TimeZoneInfo _tz;

        public MlRetrainHostedService(IServiceProvider services, ILogger<MlRetrainHostedService> logger)
        {
            _services = services;
            _logger = logger;

            try { _tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Sofia"); }
            catch { _tz = TimeZoneInfo.Utc; _logger.LogWarning("TimeZone 'Europe/Sofia' not found. Using UTC."); }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await TrainIfNeeded(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, _tz).DateTime;
                var nextLocal = NextOccurrence(nowLocal, _dailyTime);
                var delay = nextLocal - nowLocal;

                _logger.LogInformation("Next ML retrain at {Next} ({Tz})", nextLocal, _tz.Id);

                try { await Task.Delay(delay, stoppingToken); }
                catch (TaskCanceledException) { break; }

                await ForceTrain(stoppingToken);
            }
        }

        private async Task TrainIfNeeded(CancellationToken ct)
        {
            using var scope = _services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<ICreditScoringService>();
            try
            {
                await svc.TrainIfNeededAsync(ct);
                _logger.LogInformation("Initial ML training/check done.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Initial TrainIfNeededAsync failed.");
            }
        }

        private async Task ForceTrain(CancellationToken ct)
        {
            using var scope = _services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<ICreditScoringService>();
            try
            {
                await svc.ForceTrainAsync(ct);
                _logger.LogInformation("Daily ML retrain completed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Daily ML retrain failed.");
            }
        }

        private static DateTime NextOccurrence(DateTime nowLocal, TimeSpan at)
        {
            var candidate = new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day, at.Hours, at.Minutes, at.Seconds);
            return candidate <= nowLocal ? candidate.AddDays(1) : candidate;
        }
    }
}
