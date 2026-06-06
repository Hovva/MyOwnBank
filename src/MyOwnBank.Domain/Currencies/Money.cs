using MyOwnBank.Domain.Common;

namespace MyOwnBank.Domain.Currencies;

public sealed class Money
{
    public Money(string currencyCode, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            throw new DomainException("Currency code is required.");
        }

        if (amount <= 0)
        {
            throw new DomainException("Amount must be positive.");
        }

        CurrencyCode = currencyCode;
        Amount = amount;
    }

    public string CurrencyCode { get; }

    public decimal Amount { get; }
}
