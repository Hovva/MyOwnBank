using MyOwnBank.Application.Abstractions;
using MyOwnBank.Application.Banks;
using MyOwnBank.Application.Common;

namespace MyOwnBank.MiniApp.Telegram;

public sealed class NotificationDeliveryService(
    TelegramNotificationSender telegram,
    IUserNotificationRepository repository,
    IClock clock)
{
    public Task DeliverCreditAsync(Guid bankId, CardCreditedNotification notification, CancellationToken cancellationToken)
    {
        var reasonSuffix = string.IsNullOrWhiteSpace(notification.Reason)
            ? string.Empty
            : $" Комментарий: {notification.Reason}";

        return DeliverAsync(
            bankId,
            notification.RecipientTelegramUserId,
            "credit",
            "Начисление на карту",
            $"На твою карту начислено {notification.Amount} {notification.CurrencyName} от {notification.IssuerDisplayName}.{reasonSuffix}",
            telegram.SendCreditNotificationAsync(notification, cancellationToken),
            cancellationToken);
    }

    public Task DeliverFineAsync(Guid bankId, CardFinedNotification notification, CancellationToken cancellationToken) =>
        DeliverAsync(
            bankId,
            notification.RecipientTelegramUserId,
            "fine",
            "Штраф",
            $"Списано {notification.Amount} {notification.CurrencyName}. Причина: {notification.Reason}",
            telegram.SendFineNotificationAsync(notification, cancellationToken),
            cancellationToken);

    public async Task DeliverPurchaseAsync(Guid bankId, ProductPurchasedNotification notification, CancellationToken cancellationToken)
    {
        var lines = notification.Items.Select(item =>
            $"{item.ProductName} ×{item.Quantity} — {item.Price * item.Quantity} {item.CurrencyName}");
        var message = $"{notification.BuyerDisplayName} купил в магазине: {string.Join(", ", lines)}";

        await repository.AddAsync(
            new UserNotificationRecord(
                Guid.NewGuid(),
                notification.OwnerTelegramUserId,
                bankId,
                "purchase",
                "Покупка в магазине",
                message,
                clock.UtcNow,
                false),
            cancellationToken);

        if (notification.BuyerTelegramUserId != notification.OwnerTelegramUserId)
        {
            await telegram.SendPurchaseNotificationAsync(notification, cancellationToken);
        }
    }

    private async Task DeliverAsync(
        Guid bankId,
        long recipientTelegramUserId,
        string type,
        string title,
        string message,
        Task sendTelegramTask,
        CancellationToken cancellationToken)
    {
        await sendTelegramTask;
        await repository.AddAsync(
            new UserNotificationRecord(
                Guid.NewGuid(),
                recipientTelegramUserId,
                bankId,
                type,
                title,
                message,
                clock.UtcNow,
                false),
            cancellationToken);
    }
}
