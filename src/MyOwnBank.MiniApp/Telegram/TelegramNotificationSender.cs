using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyOwnBank.Application.Banks;
using MyOwnBank.MiniApp.Options;
using Telegram.Bot;

namespace MyOwnBank.MiniApp.Telegram;

public sealed class TelegramNotificationSender(
    IOptions<TelegramBotOptions> options,
    ILogger<TelegramNotificationSender> logger)
{
    public async Task SendCreditNotificationAsync(
        CardCreditedNotification notification,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.Token))
        {
            return;
        }

        try
        {
            var bot = new TelegramBotClient(options.Value.Token);
            var reasonLine = string.IsNullOrWhiteSpace(notification.Reason)
                ? string.Empty
                : $"\nКомментарий: {notification.Reason}";
            var text =
                $"""
                 💳 На твою карту начислено {notification.Amount} {notification.CurrencyName} ({notification.CurrencyCode})
                 от {notification.IssuerDisplayName}.{reasonLine}

                 Баланс: {FormatBalances(notification.NewBalances)}
                 """;

            await bot.SendMessage(notification.RecipientTelegramUserId, text, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send credit notification to {RecipientId}", notification.RecipientTelegramUserId);
        }
    }

    public async Task SendPurchaseNotificationAsync(
        ProductPurchasedNotification notification,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.Token))
        {
            return;
        }

        try
        {
            var bot = new TelegramBotClient(options.Value.Token);
            var lines = notification.Items.Select(item =>
                $"• {item.ProductName} ×{item.Quantity} — {item.Price * item.Quantity} {item.CurrencyName}");
            var text =
                $"""
                 🛒 {notification.BuyerDisplayName} купил в магазине:
                 {string.Join('\n', lines)}
                 """;

            await bot.SendMessage(notification.OwnerTelegramUserId, text, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send purchase notification to {OwnerId}", notification.OwnerTelegramUserId);
        }
    }

    public async Task SendFineNotificationAsync(
        CardFinedNotification notification,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.Token))
        {
            return;
        }

        try
        {
            var bot = new TelegramBotClient(options.Value.Token);
            var text =
                $"""
                 ⚠️ С твоей карты списано {notification.Amount} {notification.CurrencyName} ({notification.CurrencyCode})
                 от {notification.IssuerDisplayName}.

                 Причина: {notification.Reason}

                 Баланс: {FormatBalances(notification.NewBalances)}
                 """;

            await bot.SendMessage(notification.RecipientTelegramUserId, text, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send fine notification to {RecipientId}", notification.RecipientTelegramUserId);
        }
    }

    private static string FormatBalances(IReadOnlyDictionary<string, decimal> balances) =>
        string.Join(", ", balances.Select(pair => $"{pair.Key}={pair.Value}"));
}
