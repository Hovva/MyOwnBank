using MyOwnBank.Domain.Common;
using MyOwnBank.Domain.Currencies;

namespace MyOwnBank.Domain.Banks;

public sealed class BankCard
{
    private readonly Dictionary<string, decimal> _balances;

    private BankCard(Guid id, Guid ownerMemberId, Dictionary<string, decimal> balances, DateTimeOffset issuedAt)
    {
        Id = id;
        OwnerMemberId = ownerMemberId;
        IssuedAt = issuedAt;
        _balances = balances;
    }

    public Guid Id { get; }

    public Guid OwnerMemberId { get; }

    public DateTimeOffset IssuedAt { get; }

    public IReadOnlyDictionary<string, decimal> Balances => _balances;

    public static BankCard Issue(Guid ownerMemberId, IEnumerable<Currency> currencies, DateTimeOffset now)
    {
        var balances = currencies.ToDictionary(currency => currency.Code, _ => 0m, StringComparer.OrdinalIgnoreCase);

        return new BankCard(Guid.NewGuid(), ownerMemberId, balances, now);
    }

    public static BankCard Rehydrate(
        Guid id,
        Guid ownerMemberId,
        IReadOnlyDictionary<string, decimal> balances,
        DateTimeOffset issuedAt) =>
        new(id, ownerMemberId, new Dictionary<string, decimal>(balances, StringComparer.OrdinalIgnoreCase), issuedAt);

    public void Credit(Money money)
    {
        EnsureCurrencyExists(money.CurrencyCode);
        _balances[money.CurrencyCode] += money.Amount;
    }

    public void Debit(Money money)
    {
        EnsureCurrencyExists(money.CurrencyCode);

        if (_balances[money.CurrencyCode] < money.Amount)
        {
            throw new DomainException("Card has insufficient balance.");
        }

        _balances[money.CurrencyCode] -= money.Amount;
    }

    private void EnsureCurrencyExists(string currencyCode)
    {
        if (!_balances.ContainsKey(currencyCode))
        {
            throw new DomainException($"Currency '{currencyCode}' is not available in this bank.");
        }
    }
}
