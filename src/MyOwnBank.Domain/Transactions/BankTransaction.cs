using MyOwnBank.Domain.Currencies;

namespace MyOwnBank.Domain.Transactions;

public sealed class BankTransaction
{
    private BankTransaction(
        Guid id,
        Guid bankId,
        Guid cardId,
        string type,
        string currencyCode,
        decimal amount,
        string description,
        DateTimeOffset occurredAt)
    {
        Id = id;
        BankId = bankId;
        CardId = cardId;
        Type = type;
        CurrencyCode = currencyCode;
        Amount = amount;
        Description = description;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; }

    public Guid BankId { get; }

    public Guid CardId { get; }

    public string Type { get; }

    public string CurrencyCode { get; }

    public decimal Amount { get; }

    public string Description { get; }

    public DateTimeOffset OccurredAt { get; }

    public static BankTransaction CurrencyIssued(Guid bankId, Guid cardId, Money money, DateTimeOffset now) =>
        new(Guid.NewGuid(), bankId, cardId, "currency-issued", money.CurrencyCode, money.Amount, "Начисление валюты.", now);

    public static BankTransaction CurrencyIssuedToMember(
        Guid bankId,
        Guid cardId,
        Money money,
        string recipientDisplayName,
        DateTimeOffset now,
        string? reason = null)
    {
        var trimmedReason = reason?.Trim();
        var description = string.IsNullOrWhiteSpace(trimmedReason)
            ? $"Начисление {recipientDisplayName}: {money.Amount} {money.CurrencyCode}"
            : $"Начисление {recipientDisplayName}: {trimmedReason}";

        return new(
            Guid.NewGuid(),
            bankId,
            cardId,
            "currency-issued",
            money.CurrencyCode,
            money.Amount,
            description,
            now);
    }

    public static BankTransaction Purchase(
        Guid bankId,
        Guid cardId,
        Money money,
        string productName,
        string buyerDisplayName,
        DateTimeOffset now) =>
        new(
            Guid.NewGuid(),
            bankId,
            cardId,
            "purchase",
            money.CurrencyCode,
            -money.Amount,
            $"Покупка {buyerDisplayName}: {productName}",
            now);

    public static BankTransaction Fine(
        Guid bankId,
        Guid cardId,
        Money money,
        string targetDisplayName,
        string reason,
        DateTimeOffset now) =>
        new(
            Guid.NewGuid(),
            bankId,
            cardId,
            "fine",
            money.CurrencyCode,
            -money.Amount,
            $"Штраф {targetDisplayName}: {reason}",
            now);

    public static BankTransaction Rehydrate(
        Guid id,
        Guid bankId,
        Guid cardId,
        string type,
        string currencyCode,
        decimal amount,
        string description,
        DateTimeOffset occurredAt) =>
        new(id, bankId, cardId, type, currencyCode, amount, description, occurredAt);
}
