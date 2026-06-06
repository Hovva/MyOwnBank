using MyOwnBank.Domain.Common;
using MyOwnBank.Domain.Currencies;

namespace MyOwnBank.Domain.Shops;

public sealed class ShopProduct
{
    private ShopProduct(Guid id, string name, Money price, bool isActive, DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        Price = price;
        IsActive = isActive;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public string Name { get; private set; }

    public Money Price { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public static ShopProduct Create(string name, Money price, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Product name is required.");
        }

        return new ShopProduct(Guid.NewGuid(), name.Trim(), price, isActive: true, now);
    }

    public static ShopProduct Rehydrate(Guid id, string name, Money price, bool isActive, DateTimeOffset createdAt) =>
        new(id, name, price, isActive, createdAt);

    public void Archive() => IsActive = false;
}
