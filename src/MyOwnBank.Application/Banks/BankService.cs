using MyOwnBank.Application.Abstractions;
using MyOwnBank.Application.Common;
using MyOwnBank.Domain.Banks;
using MyOwnBank.Domain.Currencies;

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

        var bank = Bank.Create(command.Name, command.OwnerTelegramUserId, command.OwnerDisplayName, clock.UtcNow);
        await repository.AddAsync(bank, cancellationToken);

        return ToSummary(bank);
    }

    public async Task<BankSummary?> GetMyBankAsync(long telegramUserId, CancellationToken cancellationToken)
    {
        var bank = await repository.GetByTelegramUserIdAsync(telegramUserId, cancellationToken);
        return bank is null ? null : ToSummary(bank);
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

        bank.CreditCard(card.Id, new Money(command.CurrencyCode, command.Amount), clock.UtcNow, targetMember.DisplayName);

        await repository.SaveAsync(bank, cancellationToken);

        CardCreditedNotification? notification = targetMember.TelegramUserId == command.IssuerTelegramUserId
            ? null
            : new CardCreditedNotification(
                targetMember.TelegramUserId,
                issuer.DisplayName,
                command.CurrencyCode,
                GetCurrencyName(command.CurrencyCode),
                command.Amount,
                card.Balances);

        return new CreditResult(ToSummary(bank), notification);
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
        var product = shop.AddProduct(command.Name, new Money(command.CurrencyCode, command.Price), clock.UtcNow);

        await repository.SaveAsync(bank, cancellationToken);
        return product.Id;
    }

    public async Task BuyProductAsync(BuyProductCommand command, CancellationToken cancellationToken)
    {
        var bank = await GetUserBank(command.TelegramUserId, cancellationToken);
        var card = bank.GetCardForMember(command.TelegramUserId);
        bank.BuyProduct(card.Id, command.ProductId, clock.UtcNow);

        await repository.SaveAsync(bank, cancellationToken);
    }

    public async Task<IReadOnlyCollection<ProductSummary>> GetShopAsync(long telegramUserId, CancellationToken cancellationToken)
    {
        var bank = await GetUserBank(telegramUserId, cancellationToken);
        return ToSummary(bank).Products;
    }

    public async Task<IReadOnlyCollection<TransactionSummary>> GetHistoryAsync(long telegramUserId, CancellationToken cancellationToken)
    {
        var bank = await GetUserBank(telegramUserId, cancellationToken);
        var card = bank.GetCardForMember(telegramUserId);

        return ToSummary(bank).Transactions
            .Where(transaction => transaction.CardId == card.Id)
            .OrderByDescending(transaction => transaction.OccurredAt)
            .Take(10)
            .ToArray();
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
        Currency.DefaultCurrencies.Single(currency => currency.Code == currencyCode).Name;

    private static BankSummary ToSummary(Bank bank) =>
        new(
            bank.Id,
            bank.Name,
            bank.OwnerTelegramUserId,
            bank.Members
                .Select(member => new MemberSummary(member.Id, member.TelegramUserId, member.DisplayName))
                .ToArray(),
            bank.Cards
                .Select(card => new CardSummary(
                    card.Id,
                    card.OwnerMemberId,
                    bank.Members.Single(member => member.Id == card.OwnerMemberId).DisplayName,
                    card.Balances))
                .ToArray(),
            bank.Shop?.Products
                .Select(product => new ProductSummary(
                    product.Id,
                    product.Name,
                    product.Price.CurrencyCode,
                    product.Price.Amount,
                    product.IsActive))
                .ToArray() ?? [],
            bank.Transactions
                .OrderByDescending(transaction => transaction.OccurredAt)
                .Select(transaction => new TransactionSummary(
                    transaction.Id,
                    transaction.CardId,
                    transaction.Type,
                    transaction.CurrencyCode,
                    transaction.Amount,
                    transaction.Description,
                    transaction.OccurredAt))
                .ToArray());
}
