namespace MyOwnBank.Application.Abstractions;

public interface IUserNotificationRepository
{
    Task AddAsync(UserNotificationRecord notification, CancellationToken cancellationToken);

    Task<int> GetUnreadCountAsync(long recipientTelegramUserId, CancellationToken cancellationToken);

    Task<(IReadOnlyList<UserNotificationRecord> Items, bool HasMore)> GetPageAsync(
        long recipientTelegramUserId,
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task MarkAllReadAsync(long recipientTelegramUserId, CancellationToken cancellationToken);
}

public sealed record UserNotificationRecord(
    Guid Id,
    long RecipientTelegramUserId,
    Guid BankId,
    string Type,
    string Title,
    string Message,
    DateTimeOffset CreatedAt,
    bool IsRead);
