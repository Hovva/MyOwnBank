using System.Globalization;
using System.Text;
using MyOwnBank.Application.Abstractions;
using MyOwnBank.Application.Banks;
using MyOwnBank.Application.Common;
using MyOwnBank.Domain.Common;
using MyOwnBank.Domain.Currencies;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace MyOwnBank.Bot.Telegram;

public sealed class TelegramCommandRouter(
    BankService bankService,
    IUserNotificationRepository notificationRepository,
    IClock clock)
{
    public async Task HandleMessageAsync(ITelegramBotClient bot, Message message, CancellationToken cancellationToken)
    {
        if (message.Text is null || message.From is null)
        {
            return;
        }

        var command = message.Text.Trim();
        var userId = message.From.Id;
        var displayName = message.From.Username ?? message.From.FirstName ?? userId.ToString(CultureInfo.InvariantCulture);

        var response = await RouteCommandAsync(command, userId, displayName, cancellationToken);
        await bot.SendMessage(message.Chat.Id, response.Text, cancellationToken: cancellationToken);

        if (response.Notification is not null)
        {
            await SendCreditNotificationAsync(bot, response.Notification, cancellationToken);

            if (response.BankId is Guid bankId)
            {
                await notificationRepository.AddAsync(
                    new UserNotificationRecord(
                        Guid.NewGuid(),
                        response.Notification.RecipientTelegramUserId,
                        bankId,
                        "credit",
                        "Начисление на карту",
                        $"На твою карту начислено {response.Notification.Amount} {response.Notification.CurrencyName} от {response.Notification.IssuerDisplayName}.",
                        clock.UtcNow,
                        false),
                    cancellationToken);
            }
        }
    }

    public async Task<string> HandleTextAsync(
        string command,
        long userId,
        string displayName,
        CancellationToken cancellationToken)
    {
        var response = await RouteCommandAsync(command, userId, displayName, cancellationToken);
        return response.Text;
    }

    private async Task<CommandResponse> RouteCommandAsync(
        string command,
        long userId,
        string displayName,
        CancellationToken cancellationToken)
    {
        try
        {
            return await RouteAsync(command.Trim(), userId, displayName, cancellationToken);
        }
        catch (DomainException ex)
        {
            return new CommandResponse(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return new CommandResponse(ex.Message);
        }
    }

    private async Task<CommandResponse> RouteAsync(string command, long userId, string displayName, CancellationToken cancellationToken)
    {
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var commandName = parts.FirstOrDefault()?.Split('@')[0].ToLowerInvariant();

        if (commandName == "/start")
        {
            var bank = await bankService.GetMyBankAsync(userId, cancellationToken);
            if (bank is null)
            {
                return new CommandResponse("""
                       Привет! Это My own bank.

                       У тебя пока нет банка.
                       Создать банк: /create <название>
                       Вступить по приглашению: /join <код>
                       """);
            }

            return new CommandResponse(FormatBankStats(bank, userId));
        }

        if (commandName is "/create" or "/newbank")
        {
            return new CommandResponse(
                """
                Создай банк через Mini App: укажи название и хотя бы одну валюту.
                Вступить по приглашению: /join <код>
                """);
        }

        if (commandName == "/invite")
        {
            var invite = await bankService.CreateInviteCodeAsync(new CreateInviteCodeCommand(userId), cancellationToken);

            return new CommandResponse($"Invite code: {invite.Code}\nValid until: {invite.ExpiresAt:yyyy-MM-dd HH:mm} UTC");
        }

        if (commandName == "/join")
        {
            if (parts.Length < 2)
            {
                return new CommandResponse("Usage: /join <invite-code>");
            }

            var bank = await bankService.JoinBankByCodeAsync(
                new JoinBankByCodeCommand(parts[1], userId, displayName),
                cancellationToken);

            return new CommandResponse($"You joined '{bank.Name}'. Your card is ready.");
        }

        if (commandName == "/balance")
        {
            var bank = await bankService.GetMyBankAsync(userId, cancellationToken);
            if (bank is null)
            {
                return new CommandResponse("У тебя пока нет банка. Создать: /create <название>.");
            }

            var card = GetUserCard(bank, userId);
            return new CommandResponse($"Твой баланс:\n{FormatBalances(card.Balances)}");
        }

        if (commandName == "/members")
        {
            var bank = await bankService.GetMyBankAsync(userId, cancellationToken);
            if (bank is null)
            {
                return new CommandResponse("У тебя пока нет банка. Создать: /create <название>.");
            }

            return new CommandResponse(string.Join('\n', bank.Members.Select(member =>
                $"{member.DisplayName} (id: {member.TelegramUserId})")));
        }

        if (commandName == "/credit")
        {
            if (parts.Length < 3 || !TryParseAmount(parts[2], out var amount))
            {
                return new CommandResponse("""
                       Использование (только владелец банка):
                       /credit <валюта> <сумма> — на свою карту
                       /credit <валюта> <сумма> <участник> — на карту участника

                       Валюты: hug, kiss, spank
                       Или: обнимашки, поцелуйчики, порка
                       Участников: /members
                       """);
            }

            if (!TryResolveCurrency(parts[1], out var currencyCode))
            {
                return new CommandResponse("Неизвестная валюта. Доступны: hug, kiss, spank, обнимашки, поцелуйчики, порка.");
            }

            var targetMember = command.Split(' ', 4, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Skip(3)
                .FirstOrDefault();
            var creditResult = targetMember is null
                ? await bankService.CreditMyCardAsync(new CreditMyCardCommand(userId, currencyCode, amount), cancellationToken)
                : await bankService.CreditMemberCardAsync(
                    new CreditMemberCardCommand(userId, targetMember, currencyCode, amount),
                    cancellationToken);
            var bank = creditResult.Bank;
            var card = targetMember is null
                ? GetUserCard(bank, userId)
                : GetMemberCard(bank, targetMember);
            var ownerName = bank.Members.Single(member => member.Id == card.OwnerMemberId).DisplayName;
            var currencyName = Currency.DefaultCurrencies.Single(item => item.Code == currencyCode).Name;

            return new CommandResponse(
                $"Начислено {amount} {currencyName} ({currencyCode}) на карту {ownerName}.\nБаланс: {FormatBalances(card.Balances)}",
                creditResult.Notification,
                creditResult.Bank.Id);
        }

        if (commandName == "/openshop")
        {
            await bankService.OpenShopAsync(new OpenShopCommand(userId), cancellationToken);
            return new CommandResponse("Shop is open.");
        }

        if (commandName == "/addproduct")
        {
            if (parts.Length < 4 || !TryParseAmount(parts[2], out var price))
            {
                return new CommandResponse("Usage: /addproduct <hug|kiss|spank> <price> <product name>");
            }

            var productName = command.Split(' ', 4, StringSplitOptions.TrimEntries)[3];
            var productId = await bankService.AddProductAsync(
                new AddProductCommand(userId, productName, parts[1], price),
                cancellationToken);

            return new CommandResponse($"Product added.\nId: {productId}");
        }

        if (commandName == "/shop")
        {
            var products = await bankService.GetShopAsync(userId, cancellationToken);
            var active = products.Where(product => product.IsActive).ToArray();

            if (active.Length == 0)
            {
                return new CommandResponse("Shop is empty. Add something with /addproduct <currency> <price> <name>.");
            }

            return new CommandResponse(string.Join('\n', active.Select(product =>
                $"{product.Id} - {product.Name}: {product.Price} {product.CurrencyCode}")));
        }

        if (commandName == "/buy")
        {
            if (parts.Length < 2 || !Guid.TryParse(parts[1], out var productId))
            {
                return new CommandResponse("Usage: /buy <product-id>");
            }

            await bankService.BuyProductAsync(new BuyProductCommand(userId, productId), cancellationToken);
            return new CommandResponse("Purchased. Check /history and /balance.");
        }

        if (commandName == "/history")
        {
            var history = await bankService.GetHistoryAsync(userId, cancellationToken);
            if (history.Count == 0)
            {
                return new CommandResponse("No transactions yet.");
            }

            var builder = new StringBuilder();
            foreach (var transaction in history)
            {
                builder.AppendLine($"{transaction.OccurredAt:MM-dd HH:mm}: {transaction.Amount:+0.##;-0.##} {transaction.CurrencyCode} - {transaction.Description}");
            }

            return new CommandResponse(builder.ToString().TrimEnd());
        }

        return new CommandResponse("""
               My own bank commands:
               /start - show your bank stats
               /create <name> - create your bank
               /newbank <name> - create your bank
               /invite - create invite code
               /join <code> - join a bank
               /members - list bank members
               /balance - show card balances
               /credit <currency> <amount> [member] - owner issues currency to a card
               /openshop - open a shop
               /addproduct <currency> <price> <name> - add shop product
               /shop - list products
               /buy <product-id> - buy product
               /history - show latest transactions
               """);
    }

    private static async Task SendCreditNotificationAsync(
        ITelegramBotClient bot,
        CardCreditedNotification notification,
        CancellationToken cancellationToken)
    {
        var text =
            $"""
             На твою карту начислено {notification.Amount} {notification.CurrencyName} ({notification.CurrencyCode})
             от {notification.IssuerDisplayName}.

             Баланс: {FormatBalances(notification.NewBalances)}
             """;

        await bot.SendMessage(notification.RecipientTelegramUserId, text, cancellationToken: cancellationToken);
    }

    private sealed record CommandResponse(string Text, CardCreditedNotification? Notification = null, Guid? BankId = null);

    private static bool TryParseAmount(string raw, out decimal amount) =>
        decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out amount)
        || decimal.TryParse(raw, NumberStyles.Number, CultureInfo.GetCultureInfo("ru-RU"), out amount);

    private static string FormatBalances(IReadOnlyDictionary<string, decimal> balances) =>
        string.Join(", ", balances.Select(pair => $"{pair.Key}={pair.Value}"));

    private static string FormatBankStats(BankSummary bank, long telegramUserId)
    {
        var activeProducts = bank.Products.Count(product => product.IsActive);
        var card = GetUserCard(bank, telegramUserId);
        var recentTransactions = bank.Transactions
            .Where(transaction => transaction.CardId == card.Id)
            .OrderByDescending(transaction => transaction.OccurredAt)
            .Take(3)
            .ToArray();
        var builder = new StringBuilder()
            .AppendLine($"Банк: {bank.Name}")
            .AppendLine($"Участников: {bank.Members.Count}")
            .AppendLine($"Карт: {bank.Cards.Count}")
            .AppendLine($"Товаров в магазине: {activeProducts}")
            .AppendLine($"Твой баланс: {FormatBalances(card.Balances)}");

        if (recentTransactions.Length > 0)
        {
            builder.AppendLine("Последние операции:");
            foreach (var transaction in recentTransactions)
            {
                builder.AppendLine($"{transaction.Amount:+0.##;-0.##} {transaction.CurrencyCode} - {transaction.Description}");
            }
        }

        if (bank.OwnerTelegramUserId == telegramUserId)
        {
            builder.AppendLine()
                .AppendLine("Владелец: /members, /credit <валюта> <сумма> [участник]");
        }

        return builder
            .AppendLine()
            .AppendLine("Команды: /invite, /balance, /shop, /addproduct, /buy, /history")
            .ToString()
            .TrimEnd();
    }

    private static bool TryResolveCurrency(string raw, out string currencyCode) =>
        Currency.TryResolveCode(raw, out currencyCode);

    private static CardSummary GetUserCard(BankSummary bank, long telegramUserId)
    {
        var member = bank.Members.Single(item => item.TelegramUserId == telegramUserId);
        return bank.Cards.Single(card => card.OwnerMemberId == member.Id);
    }

    private static CardSummary GetMemberCard(BankSummary bank, string targetMember)
    {
        var normalizedTarget = targetMember.Trim().TrimStart('@');
        var member = bank.Members.Single(item =>
            string.Equals(item.DisplayName.TrimStart('@'), normalizedTarget, StringComparison.OrdinalIgnoreCase)
            || item.TelegramUserId.ToString(CultureInfo.InvariantCulture) == normalizedTarget);

        return bank.Cards.Single(card => card.OwnerMemberId == member.Id);
    }

}
