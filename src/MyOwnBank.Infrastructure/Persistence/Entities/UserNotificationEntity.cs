namespace MyOwnBank.Infrastructure.Persistence.Entities;

public sealed class UserNotificationEntity
{
    public Guid Id { get; set; }

    public long RecipientTelegramUserId { get; set; }

    public Guid BankId { get; set; }

    public string Type { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public bool IsRead { get; set; }
}
