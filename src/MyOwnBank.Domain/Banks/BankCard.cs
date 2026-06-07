using MyOwnBank.Domain.Common;
using MyOwnBank.Domain.Currencies;

namespace MyOwnBank.Domain.Banks;

public sealed class BankCard
{
    private readonly Dictionary<string, decimal> _balances;

    private BankCard(
        Guid id,
        Guid ownerMemberId,
        string cardNumber,
        string? holderName,
        Dictionary<string, decimal> balances,
        DateTimeOffset issuedAt)
    {
        Id = id;
        OwnerMemberId = ownerMemberId;
        CardNumber = cardNumber;
        HolderName = holderName;
        IssuedAt = issuedAt;
        _balances = balances;
    }

    public Guid Id { get; }

    public Guid OwnerMemberId { get; }

    public string CardNumber { get; }

    public string? HolderName { get; private set; }

    public DateTimeOffset IssuedAt { get; }

    public IReadOnlyDictionary<string, decimal> Balances => _balances;

    public static BankCard Issue(Guid ownerMemberId, IEnumerable<Currency> currencies, DateTimeOffset now)
    {
        var balances = currencies.ToDictionary(currency => currency.Code, _ => 0m, StringComparer.OrdinalIgnoreCase);

        return new BankCard(Guid.NewGuid(), ownerMemberId, CardNumberGenerator.Generate(), null, balances, now);
    }

    public static BankCard Rehydrate(
        Guid id,
        Guid ownerMemberId,
        string cardNumber,
        string? holderName,
        IReadOnlyDictionary<string, decimal> balances,
        DateTimeOffset issuedAt) =>
        new(
            id,
            ownerMemberId,
            string.IsNullOrWhiteSpace(cardNumber) ? CardNumberGenerator.Generate() : cardNumber,
            holderName,
            new Dictionary<string, decimal>(balances, StringComparer.OrdinalIgnoreCase),
            issuedAt);

    public string ResolveHolderName(string fallbackDisplayName) =>
        string.IsNullOrWhiteSpace(HolderName) ? fallbackDisplayName : HolderName.Trim();

    public void UpdateHolderName(string holderName)
    {
        var normalized = holderName.Trim();
        if (normalized.Length == 0)
        {
            throw new DomainException("Имя не может быть пустым.");
        }

        if (normalized.Length > 64)
        {
            throw new DomainException("Имя не длиннее 64 символов.");
        }

        if (normalized.Contains('\n') || normalized.Contains('\r'))
        {
            throw new DomainException("Имя должно быть одной строкой.");
        }

        HolderName = normalized;
    }

    internal void EnsureBalanceSlot(string currencyCode)
    {
        if (!_balances.ContainsKey(currencyCode))
        {
            _balances[currencyCode] = 0m;
        }
    }

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
