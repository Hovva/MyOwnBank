namespace MyOwnBank.Infrastructure.Persistence.Entities;

public sealed class BankEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public long OwnerTelegramUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<BankCurrencyEntity> Currencies { get; set; } = [];

    public ICollection<BankMemberEntity> Members { get; set; } = [];

    public ICollection<BankCardEntity> Cards { get; set; } = [];

    public ShopEntity? Shop { get; set; }

    public ICollection<BankTransactionEntity> Transactions { get; set; } = [];
}
