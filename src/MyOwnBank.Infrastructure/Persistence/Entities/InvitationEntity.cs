namespace MyOwnBank.Infrastructure.Persistence.Entities;

public sealed class InvitationEntity
{
    public string Code { get; set; } = string.Empty;

    public Guid BankId { get; set; }

    public long CreatedByTelegramUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public long? UsedByTelegramUserId { get; set; }

    public DateTimeOffset? UsedAt { get; set; }
}
