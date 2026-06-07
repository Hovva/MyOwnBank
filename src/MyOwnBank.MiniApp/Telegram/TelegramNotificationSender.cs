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
}
