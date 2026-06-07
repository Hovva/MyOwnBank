namespace MyOwnBank.Application.Banks;

public sealed record BankSummary(
    Guid Id,
    string Name,
    long OwnerTelegramUserId,
    IReadOnlyCollection<MemberSummary> Members,
    IReadOnlyCollection<CurrencySummary> Currencies,
    IReadOnlyCollection<CardSummary> Cards,
    IReadOnlyCollection<ProductSummary> Products,
    IReadOnlyCollection<TransactionSummary> Transactions);

public sealed record CurrencySummary(string Code, string Name, string Icon);

public sealed record MemberSummary(Guid Id, long TelegramUserId, string DisplayName);

public sealed record CardSummary(
    Guid Id,
    Guid OwnerMemberId,
    string OwnerDisplayName,
    string CardNumber,
    string HolderName,
    IReadOnlyDictionary<string, decimal> Balances);

public sealed record ProductSummary(
    Guid Id,
    string Name,
    string? Description,
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

public sealed record PurchasedItemSummary(
    string ProductName,
    string CurrencyCode,
    string CurrencyName,
    decimal Price,
    int Quantity);

public sealed record ProductPurchasedNotification(
    long OwnerTelegramUserId,
    string BuyerDisplayName,
    IReadOnlyList<PurchasedItemSummary> Items,
    IReadOnlyDictionary<string, decimal> BuyerNewBalances);

public sealed record BuyProductsResult(BankSummary Bank, ProductPurchasedNotification? Notification);
