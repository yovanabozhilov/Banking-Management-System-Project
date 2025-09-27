using BankingManagmentApp.Services.Forecasting;
using Microsoft.AspNetCore.Mvc;

namespace BankingManagmentApp.Controllers
{
    public class ForecastingController : Controller
    {
        private readonly ForecastService _forecastService;

        public ForecastingController(ForecastService forecastService)
        {
            _forecastService = forecastService;
        }

        public IActionResult Index()
        {
            // ================= TRANSACTIONS =================
            ViewData["TransactionVolume"] = _forecastService.ForecastTransactionVolumeMonthly();
            ViewData["AvgTransactionValue"] = _forecastService.ForecastAvgTransactionValue();
            ViewData["CashFlows"] = _forecastService.ForecastCashFlows();
            ViewData["TransactionAnomalies"] = _forecastService.DetectTransactionAnomalies();

            // ================= CARDS =================
            ViewData["CardExpenses"] = _forecastService.ForecastCardExpenses();
            ViewData["ActiveCards"] = _forecastService.ForecastActiveCardsCount();
            ViewData["CardDefaultRisk"] = _forecastService.ForecastCreditCardDefaultRisk();

            // ================= LOANS =================
            ViewData["OverdueLoansRate"] = _forecastService.ForecastOverdueLoansRate();
            ViewData["NewLoans"] = _forecastService.ForecastNewLoans();
            ViewData["RepaymentTrend"] = _forecastService.ForecastRepaymentTrend();

            // ================= CUSTOMERS =================
            ViewData["NewCustomers"] = _forecastService.ForecastNewCustomers();
            ViewData["ChurnRate"] = _forecastService.ForecastChurnRate();

            return View();
        }
    }
}
