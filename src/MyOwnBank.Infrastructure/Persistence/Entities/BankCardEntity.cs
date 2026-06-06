namespace MyOwnBank.Infrastructure.Persistence.Entities;

public sealed class BankCardEntity
{
    public Guid Id { get; set; }

    public Guid BankId { get; set; }

    public Guid OwnerMemberId { get; set; }

    public DateTimeOffset IssuedAt { get; set; }

    public ICollection<CardBalanceEntity> Balances { get; set; } = [];
}
