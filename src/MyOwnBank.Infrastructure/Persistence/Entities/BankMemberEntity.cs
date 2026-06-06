namespace MyOwnBank.Infrastructure.Persistence.Entities;

public sealed class BankMemberEntity
{
    public Guid Id { get; set; }

    public Guid BankId { get; set; }

    public long TelegramUserId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public DateTimeOffset JoinedAt { get; set; }
}
