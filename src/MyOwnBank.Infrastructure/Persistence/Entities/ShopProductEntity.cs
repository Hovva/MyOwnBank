namespace MyOwnBank.Infrastructure.Persistence.Entities;

public sealed class ShopProductEntity
{
    public Guid Id { get; set; }

    public Guid BankId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
