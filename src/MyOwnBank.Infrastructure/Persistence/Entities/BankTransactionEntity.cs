namespace MyOwnBank.Infrastructure.Persistence.Entities;

public sealed class BankTransactionEntity
{
    public Guid Id { get; set; }

    public Guid BankId { get; set; }

    public Guid CardId { get; set; }

    public string Type { get; set; } = string.Empty;

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Description { get; set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; set; }
}
