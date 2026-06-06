namespace MyOwnBank.Infrastructure.Persistence.Entities;

public sealed class BankCurrencyEntity
{
    public Guid BankId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
