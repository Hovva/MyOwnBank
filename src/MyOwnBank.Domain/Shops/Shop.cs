using MyOwnBank.Domain.Common;
using MyOwnBank.Domain.Currencies;

namespace MyOwnBank.Domain.Shops;

public sealed class Shop
{
    private readonly List<ShopProduct> _products = [];

    public Shop(Guid bankId, DateTimeOffset openedAt)
    {
        BankId = bankId;
        OpenedAt = openedAt;
    }

    public Guid BankId { get; }

    public DateTimeOffset OpenedAt { get; }

    public IReadOnlyCollection<ShopProduct> Products => _products.AsReadOnly();

    public ShopProduct AddProduct(string name, Money price, DateTimeOffset now, string? description = null)
    {
        var product = ShopProduct.Create(name, price, now, description);
        _products.Add(product);
        return product;
    }

    public static Shop Rehydrate(Guid bankId, DateTimeOffset openedAt, IEnumerable<ShopProduct> products)
    {
        var shop = new Shop(bankId, openedAt);
        shop._products.AddRange(products);

        return shop;
    }

    public ShopProduct GetActiveProduct(Guid productId)
    {
        var product = _products.SingleOrDefault(item => item.Id == productId && item.IsActive);

        return product ?? throw new DomainException("Product was not found or is inactive.");
    }

    public void ArchiveProduct(Guid productId) => GetActiveProduct(productId).Archive();
}
