// using BankingManagmentApp.Controllers;
// using BankingManagmentApp.Services.Forecasting;
// using Microsoft.AspNetCore.Mvc;
// using Moq;
// using Xunit;
// using FluentAssertions;
// using System.Collections.Generic;
// using BankingManagmentApp.Models; 

// namespace BankingManagmentApp.Tests.Controllers
// {
//     public class ForecastingControllerApiTests
//     {
//         private readonly Mock<ForecastService> _forecastServiceMock;
//         private readonly ForecastingController _controller;

//         public ForecastingControllerApiTests()
//         {
//             // Mock ForecastService
//             _forecastServiceMock = new Mock<ForecastService>(null);

//             // Setup default returns for mocked methods
//             _forecastServiceMock.Setup(f => f.ForecastTransactionVolumeMonthly())
//                 .Returns(new Dictionary<string, int> { { "2025-09", 10 } });

//             _forecastServiceMock.Setup(f => f.ForecastAvgTransactionValue())
//                 .Returns(200);

//             _forecastServiceMock.Setup(f => f.ForecastCashFlows())
//                 .Returns(new Dictionary<string, decimal> { { "2025-09", 5000 } });

//             _forecastServiceMock
//                 .Setup(f => f.DetectTransactionAnomalies())
//                 .Returns(new List<Transactions>()); 

//             _forecastServiceMock.Setup(f => f.ForecastCardExpenses())
//                 .Returns(1000);

//             _forecastServiceMock.Setup(f => f.ForecastActiveCardsCount())
//                 .Returns(5);

//             _forecastServiceMock.Setup(f => f.ForecastCreditCardDefaultRisk())
//                 .Returns(0.2);

//             _forecastServiceMock.Setup(f => f.ForecastOverdueLoansRate())
//                 .Returns(0.1);

//             _forecastServiceMock.Setup(f => f.ForecastNewLoans())
//                 .Returns(2);

//             _forecastServiceMock.Setup(f => f.ForecastRepaymentTrend())
//                 .Returns("Clients are repaying on time");

//             _forecastServiceMock.Setup(f => f.ForecastNewCustomers())
//                 .Returns(3);

//             _forecastServiceMock.Setup(f => f.ForecastChurnRate())
//                 .Returns(0.05);

//             // Inject mocked service into controller
//             _controller = new ForecastingController(_forecastServiceMock.Object);
//         }

//         [Fact]
//         public void Index_ShouldReturnViewResult_WithValidData()
//         {
//             // Act
//             var result = _controller.Index() as ViewResult;

//             // Assert
//             result.Should().NotBeNull();
//             result.ViewData["TransactionVolume"].Should().NotBeNull();
//             result.ViewData["AvgTransactionValue"].Should().Be(200);
//             result.ViewData["CardExpenses"].Should().Be(1000);
//             result.ViewData["ActiveCards"].Should().Be(5);
//             result.ViewData["CardDefaultRisk"].Should().Be(0.2);
//             result.ViewData["OverdueLoansRate"].Should().Be(0.1);
//             result.ViewData["NewLoans"].Should().Be(2);
//             result.ViewData["RepaymentTrend"].Should().Be("Clients are repaying on time");
//             result.ViewData["NewCustomers"].Should().Be(3);
//             result.ViewData["ChurnRate"].Should().Be(0.05);
//         }
//     }
// }
