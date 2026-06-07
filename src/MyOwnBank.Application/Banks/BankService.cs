using MyOwnBank.Application.Abstractions;
using MyOwnBank.Application.Common;
using MyOwnBank.Domain.Banks;
using MyOwnBank.Domain.Common;
using MyOwnBank.Domain.Currencies;
using MyOwnBank.Domain.Transactions;

namespace MyOwnBank.Application.Banks;

public sealed class BankService(
    IBankRepository repository,
    IInvitationRepository invitations,
    IInviteCodeGenerator inviteCodeGenerator,
    IClock clock)
{
    public async Task<BankSummary> CreateBankAsync(CreateBankCommand command, CancellationToken cancellationToken)
    {
        var existing = await repository.GetByTelegramUserIdAsync(command.OwnerTelegramUserId, cancellationToken);
        if (existing is not null)
        {
            return ToSummary(existing);
        }

        var currencies = BuildInitialCurrencies(command.Currencies);
        var bank = Bank.Create(
            command.Name,
            command.OwnerTelegramUserId,
            command.OwnerDisplayName,
            currencies,
            clock.UtcNow);
        await repository.AddAsync(bank, cancellationToken);

        return ToSummary(bank);
    }

    public async Task<BankSummary?> GetMyBankAsync(long telegramUserId, CancellationToken cancellationToken)
    {
        var bank = await repository.GetByTelegramUserIdAsync(telegramUserId, cancellationToken);
        return bank is null ? null : ToSummary(bank);
    }

    public async Task<BankSummary?> GetMyBankLiteAsync(long telegramUserId, CancellationToken cancellationToken)
    {
        var bank = await repository.GetByTelegramUserIdLiteAsync(telegramUserId, cancellationToken);
        return bank is null ? null : ToSummary(bank);
    }

    public async Task<TransactionsPageResult> GetCardTransactionsPageAsync(
        long telegramUserId,
        Guid bankId,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var bank = await repository.GetByIdLiteAsync(bankId, cancellationToken)
            ?? throw new InvalidOperationException($"Bank '{bankId}' was not found.");

        if (!bank.Members.Any(member => member.TelegramUserId == telegramUserId))
        {
            throw new InvalidOperationException("You are not a member of this bank.");
        }

        var card = bank.GetCardForMember(telegramUserId);
        var page = await repository.GetCardTransactionsPageAsync(bankId, card.Id, skip, take, cancellationToken);

        return new TransactionsPageResult(
            page.Transactions.Select(ToTransactionSummary).ToArray(),
            page.HasMore);
    }

    public async Task<TransactionsPageResult> GetMemberTransactionsPageAsync(
        long ownerTelegramUserId,
        Guid bankId,
        long targetTelegramUserId,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var bank = await repository.GetByIdLiteAsync(bankId, cancellationToken)
            ?? throw new InvalidOperationException($"Bank '{bankId}' was not found.");

        bank.EnsureOwner(ownerTelegramUserId);

        var member = bank.Members.SingleOrDefault(item => item.TelegramUserId == targetTelegramUserId)
            ?? throw new InvalidOperationException($"Member '{targetTelegramUserId}' was not found in this bank.");

        var card = bank.Cards.SingleOrDefault(item => item.OwnerMemberId == member.Id)
            ?? throw new InvalidOperationException("Member does not have a card yet.");

        var page = await repository.GetCardTransactionsPageAsync(bankId, card.Id, skip, take, cancellationToken);

        return new TransactionsPageResult(
            page.Transactions.Select(ToTransactionSummary).ToArray(),
            page.HasMore);
    }

    public async Task<InviteCodeResult> CreateInviteCodeAsync(CreateInviteCodeCommand command, CancellationToken cancellationToken)
    {
        var bank = await GetUserBank(command.CreatedByTelegramUserId, cancellationToken);
        var now = clock.UtcNow;
        var invitation = new BankInvitation(
            inviteCodeGenerator.CreateCode(),
            bank.Id,
            command.CreatedByTelegramUserId,
            now,
            now.AddDays(7),
            UsedByTelegramUserId: null,
            UsedAt: null);

        await invitations.AddAsync(invitation, cancellationToken);
        return new InviteCodeResult(invitation.Code, invitation.ExpiresAt);
    }

    public async Task<BankSummary> JoinBankByCodeAsync(JoinBankByCodeCommand command, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var invitation = await invitations.GetActiveByCodeAsync(command.Code, now, cancellationToken)
            ?? throw new InvalidOperationException("Invite code is invalid, expired, or already used.");

        var bank = await GetBank(invitation.BankId, cancellationToken);
        var member = bank.AddMember(command.TelegramUserId, command.DisplayName, now);
        bank.IssueCard(member.Id, clock.UtcNow);

        await repository.SaveAsync(bank, cancellationToken);
        await invitations.MarkUsedAsync(invitation.Code, command.TelegramUserId, now, cancellationToken);

        return ToSummary(bank);
    }

    public async Task<BankSummary> IssueCardAsync(IssueCardCommand command, CancellationToken cancellationToken)
    {
        var bank = await GetBank(command.BankId, cancellationToken);
        bank.IssueCard(command.OwnerMemberId, clock.UtcNow);

        await repository.SaveAsync(bank, cancellationToken);
        return ToSummary(bank);
    }

    public async Task<BankSummary> CreditCardAsync(CreditCardCommand command, CancellationToken cancellationToken)
    {
        var bank = await GetBank(command.BankId, cancellationToken);
        bank.CreditCard(command.CardId, new Money(command.CurrencyCode, command.Amount), clock.UtcNow);

        await repository.SaveAsync(bank, cancellationToken);
        return ToSummary(bank);
    }

    public async Task<CreditResult> CreditMyCardAsync(CreditMyCardCommand command, CancellationToken cancellationToken)
    {
        var bank = await GetUserBank(command.TelegramUserId, cancellationToken);
        bank.EnsureOwner(command.TelegramUserId);
        var owner = bank.GetMember(command.TelegramUserId);
        var card = bank.GetCardForMember(command.TelegramUserId);
        bank.CreditCard(card.Id, new Money(command.CurrencyCode, command.Amount), clock.UtcNow, owner.DisplayName);

        await repository.SaveAsync(bank, cancellationToken);
        return new CreditResult(ToSummary(bank), null);
    }

    public async Task<CreditResult> CreditMemberCardAsync(CreditMemberCardCommand command, CancellationToken cancellationToken)
    {
        var bank = await GetUserBank(command.IssuerTelegramUserId, cancellationToken);
        bank.EnsureOwner(command.IssuerTelegramUserId);
        var issuer = bank.GetMember(command.IssuerTelegramUserId);
        var targetMember = FindMember(bank, command.TargetMember);
        var card = bank.Cards.SingleOrDefault(item => item.OwnerMemberId == targetMember.Id)
            ?? bank.IssueCard(targetMember.Id, clock.UtcNow);

        bank.CreditCard(
            card.Id,
            new Money(command.CurrencyCode, command.Amount),
            clock.UtcNow,
            targetMember.DisplayName,
            command.Reason);

        await repository.SaveAsync(bank, cancellationToken);

        var trimmedReason = command.Reason?.Trim();
        CardCreditedNotification? notification = targetMember.TelegramUserId == command.IssuerTelegramUserId
            ? null
            : new CardCreditedNotification(
                targetMember.TelegramUserId,
                issuer.DisplayName,
                command.CurrencyCode,
                ResolveCurrencyName(bank, command.CurrencyCode),
                command.Amount,
                string.IsNullOrWhiteSpace(trimmedReason) ? null : trimmedReason,
                card.Balances);

        return new CreditResult(ToSummary(bank), notification);
    }

    public async Task<FineResult> FineMemberCardAsync(FineMemberCardCommand command, CancellationToken cancellationToken)
    {
        var bank = await GetUserBank(command.IssuerTelegramUserId, cancellationToken);
        bank.EnsureOwner(command.IssuerTelegramUserId);
        var issuer = bank.GetMember(command.IssuerTelegramUserId);
        var targetMember = FindMember(bank, command.TargetMember);
        var card = bank.Cards.SingleOrDefault(item => item.OwnerMemberId == targetMember.Id)
            ?? bank.IssueCard(targetMember.Id, clock.UtcNow);

        bank.FineCard(card.Id, new Money(command.CurrencyCode, command.Amount), clock.UtcNow, targetMember.DisplayName, command.Reason);

        await repository.SaveAsync(bank, cancellationToken);

        CardFinedNotification? notification = targetMember.TelegramUserId == command.IssuerTelegramUserId
            ? null
            : new CardFinedNotification(
                targetMember.TelegramUserId,
                issuer.DisplayName,
                command.CurrencyCode,
                ResolveCurrencyName(bank, command.CurrencyCode),
                command.Amount,
                command.Reason.Trim(),
                card.Balances);

        return new FineResult(ToSummary(bank), notification);
    }

    public async Task OpenShopAsync(OpenShopCommand command, CancellationToken cancellationToken)
    {
        var bank = await GetUserBank(command.TelegramUserId, cancellationToken);
        bank.OpenShop(clock.UtcNow);

        await repository.SaveAsync(bank, cancellationToken);
    }

    public async Task<Guid> AddProductAsync(AddProductCommand command, CancellationToken cancellationToken)
    {
        var bank = await GetUserBank(command.TelegramUserId, cancellationToken);
        var shop = bank.OpenShop(clock.UtcNow);
        var product = shop.AddProduct(
            command.Name,
            new Money(command.CurrencyCode, command.Price),
            clock.UtcNow,
            command.Description);

        await repository.SaveAsync(bank, cancellationToken);
        return product.Id;
    }

    public async Task BuyProductAsync(BuyProductCommand command, CancellationToken cancellationToken)
    {
        await BuyProductsAsync(new BuyProductsCommand(command.TelegramUserId, [command.ProductId]), cancellationToken);
    }

    public async Task<BuyProductsResult> BuyProductsAsync(BuyProductsCommand command, CancellationToken cancellationToken)
    {
        if (command.ProductIds is null || command.ProductIds.Count == 0)
        {
            throw new DomainException("Корзина пуста.");
        }

        var bank = await GetUserBank(command.TelegramUserId, cancellationToken);
        var shop = bank.Shop ?? throw new DomainException("Магазин ещё не открыт.");
        var card = bank.GetCardForMember(command.TelegramUserId);
        var buyer = bank.GetMember(command.TelegramUserId);
        var now = clock.UtcNow;

        var required = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var snapshots = new List<(Guid Id, string Name, Money Price)>();

        foreach (var productId in command.ProductIds)
        {
            var product = shop.GetActiveProduct(productId);
            snapshots.Add((product.Id, product.Name, product.Price));
            required[product.Price.CurrencyCode] = required.GetValueOrDefault(product.Price.CurrencyCode) + product.Price.Amount;
        }

        foreach (var (currencyCode, amount) in required)
        {
            if (card.Balances.GetValueOrDefault(currencyCode) < amount)
            {
                var currencyName = ResolveCurrencyName(bank, currencyCode);
                throw new DomainException($"Недостаточно {currencyName}.");
            }
        }

        foreach (var snapshot in snapshots)
        {
            bank.BuyProduct(card.Id, snapshot.Id, now, buyer.DisplayName);
        }

        await repository.SaveAsync(bank, cancellationToken);

        var groupedItems = snapshots
            .GroupBy(item => (item.Name, item.Price.CurrencyCode, item.Price.Amount))
            .Select(group => new PurchasedItemSummary(
                group.Key.Name,
                group.Key.CurrencyCode,
                ResolveCurrencyName(bank, group.Key.CurrencyCode),
                group.Key.Amount,
                group.Count()))
            .ToArray();

        var notification = new ProductPurchasedNotification(
            bank.OwnerTelegramUserId,
            buyer.TelegramUserId,
            buyer.DisplayName,
            groupedItems,
            card.Balances);

        return new BuyProductsResult(ToSummary(bank), notification);
    }

    public async Task<PurchasesPageResult> GetBankPurchasesPageAsync(
        long ownerTelegramUserId,
        Guid bankId,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var bank = await repository.GetByIdLiteAsync(bankId, cancellationToken)
            ?? throw new InvalidOperationException($"Bank '{bankId}' was not found.");

        bank.EnsureOwner(ownerTelegramUserId);

        var page = await repository.GetBankTransactionsByTypePageAsync(bankId, "purchase", skip, take, cancellationToken);
        var purchases = page.Transactions
            .Select(transaction =>
            {
                var card = bank.Cards.Single(item => item.Id == transaction.CardId);
                var member = bank.Members.Single(item => item.Id == card.OwnerMemberId);
                return new PurchaseHistoryItem(
                    transaction.Id,
                    member.DisplayName,
                    member.TelegramUserId,
                    ExtractPurchaseProductName(transaction.Description),
                    transaction.CurrencyCode,
                    ResolveCurrencyName(bank, transaction.CurrencyCode),
                    Math.Abs(transaction.Amount),
                    transaction.OccurredAt);
            })
            .ToArray();

        return new PurchasesPageResult(purchases, page.HasMore);
    }

    private static string ExtractPurchaseProductName(string description)
    {
        const string legacyPrefix = "Purchased: ";
        if (description.StartsWith(legacyPrefix, StringComparison.Ordinal))
        {
            return description[legacyPrefix.Length..];
        }

        var separatorIndex = description.IndexOf(": ", StringComparison.Ordinal);
        return separatorIndex >= 0 && separatorIndex < description.Length - 2
            ? description[(separatorIndex + 2)..]
            : description;
    }

    public async Task<IReadOnlyCollection<ProductSummary>> GetShopAsync(long telegramUserId, CancellationToken cancellationToken)
    {
        var bank = await GetUserBank(telegramUserId, cancellationToken);
        return ToSummary(bank).Products;
    }

    public async Task RemoveProductAsync(RemoveProductCommand command, CancellationToken cancellationToken)
    {
        var bank = await GetUserBank(command.TelegramUserId, cancellationToken);
        bank.EnsureOwner(command.TelegramUserId);
        var shop = bank.Shop ?? throw new DomainException("Магазин ещё не открыт.");
        shop.ArchiveProduct(command.ProductId);

        await repository.SaveAsync(bank, cancellationToken);
    }

    public async Task<BankSummary> AddCurrencyAsync(AddCurrencyCommand command, CancellationToken cancellationToken)
    {
        var bank = await GetUserBank(command.TelegramUserId, cancellationToken);
        bank.EnsureOwner(command.TelegramUserId);

        var code = NormalizeCurrencyCode(command.Code);
        var name = command.Name.Trim();
        var icon = command.Icon.Trim();

        if (name.Length == 0)
        {
            throw new DomainException("Укажи название валюты.");
        }

        if (icon.Length == 0)
        {
            throw new DomainException("Укажи иконку валюты.");
        }

        bank.AddCurrency(new Currency(code, name, CurrencyIcon.Normalize(icon)));

        await repository.SaveAsync(bank, cancellationToken);
        return ToSummary(bank);
    }

    public async Task<BankSummary> UpdateCurrencyAsync(UpdateCurrencyCommand command, CancellationToken cancellationToken)
    {
        var bank = await GetUserBank(command.TelegramUserId, cancellationToken);
        bank.EnsureOwner(command.TelegramUserId);
        bank.UpdateCurrency(command.CurrencyCode, command.Name, command.Icon);

        await repository.SaveAsync(bank, cancellationToken);
        return ToSummary(bank);
    }

    public async Task<BankSummary> UpdateCardHolderNameAsync(UpdateCardHolderNameCommand command, CancellationToken cancellationToken)
    {
        var bank = await GetUserBank(command.TelegramUserId, cancellationToken);
        var card = bank.GetCardForMember(command.TelegramUserId);
        card.UpdateHolderName(command.HolderName);

        await repository.SaveAsync(bank, cancellationToken);
        return ToSummary(bank);
    }

    public async Task<Guid> DeleteBankAsync(DeleteBankCommand command, CancellationToken cancellationToken)
    {
        var bank = await GetUserBank(command.TelegramUserId, cancellationToken);
        bank.EnsureOwner(command.TelegramUserId);

        if (command.BankId is Guid bankId && bank.Id != bankId)
        {
            throw new DomainException("Банк не найден.");
        }

        await repository.DeleteAsync(bank.Id, cancellationToken);
        return bank.Id;
    }

    public async Task<IReadOnlyCollection<TransactionSummary>> GetHistoryAsync(long telegramUserId, CancellationToken cancellationToken)
    {
        var bank = await repository.GetByTelegramUserIdLiteAsync(telegramUserId, cancellationToken)
            ?? throw new InvalidOperationException("You do not have a bank yet. Use /newbank <name> or /join <code>.");

        var page = await GetCardTransactionsPageAsync(telegramUserId, bank.Id, 0, 10, cancellationToken);
        return page.Transactions;
    }

    private async Task<Bank> GetBank(Guid bankId, CancellationToken cancellationToken) =>
        await repository.GetByIdAsync(bankId, cancellationToken)
        ?? throw new InvalidOperationException($"Bank '{bankId}' was not found.");

    private async Task<Bank> GetUserBank(long telegramUserId, CancellationToken cancellationToken) =>
        await repository.GetByTelegramUserIdAsync(telegramUserId, cancellationToken)
        ?? throw new InvalidOperationException("You do not have a bank yet. Use /newbank <name> or /join <code>.");

    private static BankMember FindMember(Bank bank, string targetMember)
    {
        var normalizedTarget = targetMember.Trim().TrimStart('@');
        if (long.TryParse(normalizedTarget, out var telegramUserId))
        {
            return bank.Members.SingleOrDefault(member => member.TelegramUserId == telegramUserId)
                ?? throw new InvalidOperationException($"Member '{targetMember}' was not found in this bank.");
        }

        return bank.Members.SingleOrDefault(member =>
                string.Equals(member.DisplayName.TrimStart('@'), normalizedTarget, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Member '{targetMember}' was not found in this bank.");
    }

    private static string GetCurrencyName(string currencyCode) =>
        Currency.DefaultCurrencies.SingleOrDefault(currency => currency.Code == currencyCode)?.Name
        ?? currencyCode;

    private static string ResolveCurrencyName(Bank bank, string currencyCode) =>
        bank.Currencies.SingleOrDefault(currency => currency.Code.Equals(currencyCode, StringComparison.OrdinalIgnoreCase))?.Name
        ?? currencyCode;

    private static IReadOnlyList<Currency> BuildInitialCurrencies(IReadOnlyList<CreateBankCurrencyInput> currencies)
    {
        if (currencies is null || currencies.Count == 0)
        {
            throw new DomainException("Добавь хотя бы одну валюту.");
        }

        if (currencies.Count > Bank.MaxCurrencies)
        {
            throw new DomainException($"В банке не больше {Bank.MaxCurrencies} валют.");
        }

        return currencies
            .Select(item =>
            {
                var code = NormalizeCurrencyCode(item.Code);
                var name = item.Name.Trim();
                var icon = item.Icon.Trim();

                if (name.Length == 0)
                {
                    throw new DomainException("Укажи название валюты.");
                }

                if (icon.Length == 0)
                {
                    throw new DomainException("Укажи иконку валюты.");
                }

                return new Currency(code, name, CurrencyIcon.Normalize(icon));
            })
            .ToArray();
    }

    private static string NormalizeCurrencyCode(string code)
    {
        var normalized = code.Trim().ToLowerInvariant();
        if (normalized.Length is < 2 or > 32)
        {
            throw new DomainException("Код валюты: от 2 до 32 латинских символов.");
        }

        if (!normalized.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_'))
        {
            throw new DomainException("Код валюты: только латиница, цифры, - и _.");
        }

        return normalized;
    }

    private static BankSummary ToSummary(Bank bank) =>
        new(
            bank.Id,
            bank.Name,
            bank.OwnerTelegramUserId,
            bank.Members
                .Select(member => new MemberSummary(member.Id, member.TelegramUserId, member.DisplayName))
                .ToArray(),
            bank.Currencies
                .Select(currency => new CurrencySummary(currency.Code, currency.Name, currency.ResolveIcon()))
                .ToArray(),
            bank.Cards
                .Select(card =>
                {
                    var owner = bank.Members.Single(member => member.Id == card.OwnerMemberId);
                    return new CardSummary(
                        card.Id,
                        card.OwnerMemberId,
                        owner.DisplayName,
                        card.CardNumber,
                        card.ResolveHolderName(owner.DisplayName),
                        card.Balances);
                })
                .ToArray(),
            bank.Shop?.Products
                .Select(product => new ProductSummary(
                    product.Id,
                    product.Name,
                    product.Description,
                    product.Price.CurrencyCode,
                    product.Price.Amount,
                    product.IsActive))
                .ToArray() ?? [],
            bank.Transactions
                .OrderByDescending(transaction => transaction.OccurredAt)
                .Select(ToTransactionSummary)
                .ToArray());

    private static TransactionSummary ToTransactionSummary(BankTransaction transaction) =>
        new(
            transaction.Id,
            transaction.CardId,
            transaction.Type,
            transaction.CurrencyCode,
            transaction.Amount,
            transaction.Description,
            transaction.OccurredAt);
}
