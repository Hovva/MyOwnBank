namespace MyOwnBank.Application.Banks;

public sealed record CreateBankCommand(string Name, long OwnerTelegramUserId, string OwnerDisplayName);

public sealed record CreateInviteCodeCommand(long CreatedByTelegramUserId);

public sealed record JoinBankByCodeCommand(string Code, long TelegramUserId, string DisplayName);

public sealed record IssueCardCommand(Guid BankId, Guid OwnerMemberId);

public sealed record CreditMyCardCommand(long TelegramUserId, string CurrencyCode, decimal Amount);

public sealed record CreditMemberCardCommand(long IssuerTelegramUserId, string TargetMember, string CurrencyCode, decimal Amount);

public sealed record CreditCardCommand(Guid BankId, Guid CardId, string CurrencyCode, decimal Amount);

public sealed record OpenShopCommand(long TelegramUserId);

public sealed record AddProductCommand(long TelegramUserId, string Name, string CurrencyCode, decimal Price);

public sealed record BuyProductCommand(long TelegramUserId, Guid ProductId);
