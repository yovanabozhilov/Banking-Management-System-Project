using System;

namespace BankingManagmentApp.Services.Approval
{
    public enum ProductType
    {
        Personal = 0,
        Mortgage = 1,
        Auto = 2,
        CreditCard = 3
    }

    public sealed class ApplicationFeatures
    {
        public decimal RequestedAmount { get; init; }
        public int TermMonths { get; init; }
        public ProductType Product { get; init; }
    }
}
