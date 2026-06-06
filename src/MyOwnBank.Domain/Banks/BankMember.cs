namespace MyOwnBank.Domain.Banks;

public sealed record BankMember(Guid Id, long TelegramUserId, string DisplayName, DateTimeOffset JoinedAt)
{
    public static BankMember Create(long telegramUserId, string displayName, DateTimeOffset now) =>
        new(Guid.NewGuid(), telegramUserId, displayName.Trim(), now);

    public static BankMember Rehydrate(Guid id, long telegramUserId, string displayName, DateTimeOffset joinedAt) =>
        new(id, telegramUserId, displayName, joinedAt);
}
