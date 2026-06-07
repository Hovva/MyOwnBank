using MyOwnBank.Domain.Common;
using MyOwnBank.Domain.Currencies;

namespace MyOwnBank.Domain.Shops;

public sealed class ShopProduct
{
    public const int MaxDescriptionLength = 512;

    private ShopProduct(Guid id, string name, string? description, Money price, bool isActive, DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        Description = description;
        Price = price;
        IsActive = isActive;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public Money Price { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public static ShopProduct Create(string name, Money price, DateTimeOffset now, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Product name is required.");
        }

        return new ShopProduct(
            Guid.NewGuid(),
            name.Trim(),
            NormalizeDescription(description),
            price,
            isActive: true,
            now);
    }

    public static ShopProduct Rehydrate(
        Guid id,
        string name,
        string? description,
        Money price,
        bool isActive,
        DateTimeOffset createdAt) =>
        new(id, name, NormalizeDescription(description), price, isActive, createdAt);

    private static string? NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var trimmed = description.Trim();
        if (trimmed.Length > MaxDescriptionLength)
        {
            throw new DomainException($"Описание товара не длиннее {MaxDescriptionLength} символов.");
        }

        return trimmed;
    }

    public void Archive() => IsActive = false;
}
