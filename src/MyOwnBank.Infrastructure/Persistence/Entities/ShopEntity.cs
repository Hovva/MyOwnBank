namespace MyOwnBank.Infrastructure.Persistence.Entities;

public sealed class ShopEntity
{
    public Guid BankId { get; set; }

    public DateTimeOffset OpenedAt { get; set; }

    public ICollection<ShopProductEntity> Products { get; set; } = [];
}
