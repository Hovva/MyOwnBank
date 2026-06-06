namespace MyOwnBank.Bot.Options;

public sealed class TelegramBotOptions
{
    public const string SectionName = "TelegramBot";

    public string Token { get; init; } = string.Empty;
}
