namespace MyOwnBank.Infrastructure.Persistence.Entities;

public sealed class CardBalanceEntity
{
    public Guid CardId { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal Amount { get; set; }
}
