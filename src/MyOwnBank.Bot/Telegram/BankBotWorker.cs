using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyOwnBank.Bot.Options;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MyOwnBank.Bot.Telegram;

public sealed class BankBotWorker(
    IOptions<TelegramBotOptions> options,
    TelegramCommandRouter router,
    ILogger<BankBotWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.Token))
        {
            logger.LogWarning("Telegram bot token is empty. Telegram polling is disabled; web test UI is still available.");
            return;
        }

        var bot = new TelegramBotClient(options.Value.Token);
        var me = await bot.GetMe(stoppingToken);
        await bot.DeleteWebhook(dropPendingUpdates: true, cancellationToken: stoppingToken);

        logger.LogInformation("Telegram polling started for @{Username}. Webhook cleared.", me.Username);

        bot.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            new ReceiverOptions { AllowedUpdates = [UpdateType.Message] },
            stoppingToken);

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken cancellationToken)
    {
        if (update.Message is not null)
        {
            logger.LogInformation(
                "Received Telegram message from {UserId}: {Text}",
                update.Message.From?.Id,
                update.Message.Text);

            await router.HandleMessageAsync(bot, update.Message, cancellationToken);
        }
    }

    private Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Telegram polling failed.");
        return Task.CompletedTask;
    }
}
