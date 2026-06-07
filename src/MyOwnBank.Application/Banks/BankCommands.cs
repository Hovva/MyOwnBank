namespace MyOwnBank.Application.Banks;

public sealed record CreateBankCurrencyInput(string Code, string Name, string Icon);

public sealed record CreateBankCommand(
    string Name,
    long OwnerTelegramUserId,
    string OwnerDisplayName,
    IReadOnlyList<CreateBankCurrencyInput> Currencies);

public sealed record CreateInviteCodeCommand(long CreatedByTelegramUserId);

public sealed record JoinBankByCodeCommand(string Code, long TelegramUserId, string DisplayName);

public sealed record IssueCardCommand(Guid BankId, Guid OwnerMemberId);

public sealed record CreditMyCardCommand(long TelegramUserId, string CurrencyCode, decimal Amount);

public sealed record CreditMemberCardCommand(long IssuerTelegramUserId, string TargetMember, string CurrencyCode, decimal Amount);

public sealed record CreditCardCommand(Guid BankId, Guid CardId, string CurrencyCode, decimal Amount);

public sealed record OpenShopCommand(long TelegramUserId);

public sealed record AddProductCommand(long TelegramUserId, string Name, string CurrencyCode, decimal Price, string? Description = null);

public sealed record BuyProductCommand(long TelegramUserId, Guid ProductId);

public sealed record BuyProductsCommand(long TelegramUserId, IReadOnlyList<Guid> ProductIds);

public sealed record RemoveProductCommand(long TelegramUserId, Guid ProductId);

public sealed record UpdateCardHolderNameCommand(long TelegramUserId, string HolderName);

public sealed record UpdateCurrencyCommand(long TelegramUserId, string CurrencyCode, string Name, string Icon);

public sealed record AddCurrencyCommand(long TelegramUserId, string Code, string Name, string Icon);

public sealed record DeleteBankCommand(long TelegramUserId, Guid? BankId = null);
