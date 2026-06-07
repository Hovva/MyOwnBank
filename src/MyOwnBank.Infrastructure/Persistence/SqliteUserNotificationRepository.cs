using Microsoft.EntityFrameworkCore;
using MyOwnBank.Application.Abstractions;
using MyOwnBank.Infrastructure.Persistence.Entities;

namespace MyOwnBank.Infrastructure.Persistence;

public sealed class SqliteUserNotificationRepository(IDbContextFactory<MyOwnBankDbContext> dbContextFactory)
    : IUserNotificationRepository
{
    public async Task AddAsync(UserNotificationRecord notification, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        db.UserNotifications.Add(new UserNotificationEntity
        {
            Id = notification.Id,
            RecipientTelegramUserId = notification.RecipientTelegramUserId,
            BankId = notification.BankId,
            Type = notification.Type,
            Title = notification.Title,
            Message = notification.Message,
            CreatedAt = notification.CreatedAt,
            IsRead = notification.IsRead
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> GetUnreadCountAsync(long recipientTelegramUserId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.UserNotifications
            .AsNoTracking()
            .CountAsync(item => item.RecipientTelegramUserId == recipientTelegramUserId && !item.IsRead, cancellationToken);
    }

    public async Task<(IReadOnlyList<UserNotificationRecord> Items, bool HasMore)> GetPageAsync(
        long recipientTelegramUserId,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        if (take <= 0)
        {
            return ([], false);
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await db.UserNotifications
            .AsNoTracking()
            .Where(item => item.RecipientTelegramUserId == recipientTelegramUserId)
            .OrderByDescending(item => item.CreatedAt)
            .Skip(skip)
            .Take(take + 1)
            .ToListAsync(cancellationToken);

        var hasMore = entities.Count > take;
        if (hasMore)
        {
            entities.RemoveAt(entities.Count - 1);
        }

        return (entities.Select(ToRecord).ToArray(), hasMore);
    }

    public async Task MarkAllReadAsync(long recipientTelegramUserId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await db.UserNotifications
            .Where(item => item.RecipientTelegramUserId == recipientTelegramUserId && !item.IsRead)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.IsRead, true),
                cancellationToken);
    }

    private static UserNotificationRecord ToRecord(UserNotificationEntity entity) =>
        new(
            entity.Id,
            entity.RecipientTelegramUserId,
            entity.BankId,
            entity.Type,
            entity.Title,
            entity.Message,
            entity.CreatedAt,
            entity.IsRead);
}
