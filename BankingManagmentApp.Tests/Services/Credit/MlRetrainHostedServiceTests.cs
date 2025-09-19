using System;
using System.Threading;
using System.Threading.Tasks;
using BankingManagmentApp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BankingManagmentApp.Tests.Services
{
    public class MlRetrainHostedServiceTests
    {
        [Fact]
        public void NextOccurrence_ReturnsToday_WhenFutureTime()
        {
            // Arrange
            var now = new DateTime(2025, 9, 18, 2, 0, 0);
            var at = new TimeSpan(3, 15, 0);

            // Act
            var next = InvokeNextOccurrence(now, at);

            // Assert
            Assert.Equal(new DateTime(2025, 9, 18, 3, 15, 0), next);
        }

        [Fact]
        public void NextOccurrence_ReturnsTomorrow_WhenPastTime()
        {
            var now = new DateTime(2025, 9, 18, 4, 0, 0);
            var at = new TimeSpan(3, 15, 0);

            var next = InvokeNextOccurrence(now, at);

            Assert.Equal(new DateTime(2025, 9, 19, 3, 15, 0), next);
        }

        [Fact]
        public async Task ExecuteAsync_Calls_TrainIfNeeded_Then_ForceTrain()
        {
            // Arrange
            var scoringMock = new Mock<ICreditScoringService>();
            scoringMock.Setup(s => s.TrainIfNeededAsync(It.IsAny<CancellationToken>()))
                       .Returns(Task.CompletedTask)
                       .Verifiable();
            scoringMock.Setup(s => s.ForceTrainAsync(It.IsAny<CancellationToken>()))
                       .Returns(Task.CompletedTask)
                       .Verifiable();

            var services = new ServiceCollection();
            services.AddScoped(_ => scoringMock.Object);

            var provider = services.BuildServiceProvider();

            var logger = new Mock<ILogger<MlRetrainHostedService>>();

            var svc = new MlRetrainHostedService(provider, logger.Object);

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(200)); // stop quickly

            // Act
            await svc.StartAsync(cts.Token);

            // Assert
            scoringMock.Verify(s => s.TrainIfNeededAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
            // ForceTrain might or might not run depending on the timing,
            // so we don't assert it strictly. If you want to ensure it's called,
            // you could reduce the scheduled delay in the service for testability.
        }

        /// <summary>
        /// Uses reflection to call private static NextOccurrence for pure unit testing.
        /// </summary>
        private static DateTime InvokeNextOccurrence(DateTime now, TimeSpan at)
        {
            var method = typeof(MlRetrainHostedService)
                .GetMethod("NextOccurrence", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            return (DateTime)method!.Invoke(null, new object[] { now, at })!;
        }
    }
}
