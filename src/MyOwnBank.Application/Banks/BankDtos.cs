namespace MyOwnBank.Application.Banks;

public sealed record BankSummary(
    Guid Id,
    string Name,
    long OwnerTelegramUserId,
    IReadOnlyCollection<MemberSummary> Members,
    IReadOnlyCollection<CardSummary> Cards,
    IReadOnlyCollection<ProductSummary> Products,
    IReadOnlyCollection<TransactionSummary> Transactions);

public sealed record MemberSummary(Guid Id, long TelegramUserId, string DisplayName);

public sealed record CardSummary(
    Guid Id,
    Guid OwnerMemberId,
    string OwnerDisplayName,
    IReadOnlyDictionary<string, decimal> Balances);

public sealed record ProductSummary(
    Guid Id,
    string Name,
    string CurrencyCode,
    decimal Price,
    bool IsActive);

public sealed record TransactionSummary(
    Guid Id,
    Guid CardId,
    string Type,
    string CurrencyCode,
    decimal Amount,
    string Description,
    DateTimeOffset OccurredAt);

public sealed record InviteCodeResult(string Code, DateTimeOffset ExpiresAt);

public sealed record CardCreditedNotification(
    long RecipientTelegramUserId,
    string IssuerDisplayName,
    string CurrencyCode,
    string CurrencyName,
    decimal Amount,
    IReadOnlyDictionary<string, decimal> NewBalances);

public sealed record CreditResult(BankSummary Bank, CardCreditedNotification? Notification);
