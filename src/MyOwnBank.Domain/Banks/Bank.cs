using MyOwnBank.Domain.Common;
using MyOwnBank.Domain.Currencies;
using MyOwnBank.Domain.Shops;
using MyOwnBank.Domain.Transactions;

namespace MyOwnBank.Domain.Banks;

public sealed class Bank
{
    private readonly List<BankMember> _members = [];
    private readonly List<BankCard> _cards = [];
    private readonly List<Currency> _currencies = [];
    private readonly List<BankTransaction> _transactions = [];

    private Bank(Guid id, string name, long ownerTelegramUserId, DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        OwnerTelegramUserId = ownerTelegramUserId;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public string Name { get; private set; }

    public long OwnerTelegramUserId { get; }

    public DateTimeOffset CreatedAt { get; }

    public Shop? Shop { get; private set; }

    public IReadOnlyCollection<BankMember> Members => _members.AsReadOnly();

    public IReadOnlyCollection<BankCard> Cards => _cards.AsReadOnly();

    public IReadOnlyCollection<Currency> Currencies => _currencies.AsReadOnly();

    public IReadOnlyCollection<BankTransaction> Transactions => _transactions.AsReadOnly();

    public static Bank Create(string name, long ownerTelegramUserId, string ownerDisplayName, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Bank name is required.");
        }

        var bank = new Bank(Guid.NewGuid(), name.Trim(), ownerTelegramUserId, now);
        bank._currencies.AddRange(Currency.DefaultCurrencies);
        var owner = bank.AddMember(ownerTelegramUserId, ownerDisplayName, now);
        bank.IssueCard(owner.Id, now);

        return bank;
    }

    public static Bank Rehydrate(
        Guid id,
        string name,
        long ownerTelegramUserId,
        DateTimeOffset createdAt,
        IEnumerable<Currency> currencies,
        IEnumerable<BankMember> members,
        IEnumerable<BankCard> cards,
        Shop? shop,
        IEnumerable<BankTransaction> transactions)
    {
        var bank = new Bank(id, name, ownerTelegramUserId, createdAt)
        {
            Shop = shop
        };

        bank._currencies.AddRange(currencies);
        bank._members.AddRange(members);
        bank._cards.AddRange(cards);
        bank._transactions.AddRange(transactions);

        return bank;
    }

    public BankMember AddMember(long telegramUserId, string displayName, DateTimeOffset now)
    {
        var existing = _members.SingleOrDefault(member => member.TelegramUserId == telegramUserId);
        if (existing is not null)
        {
            return existing;
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new DomainException("Member display name is required.");
        }

        var member = BankMember.Create(telegramUserId, displayName, now);
        _members.Add(member);
        return member;
    }

    public BankCard IssueCard(Guid ownerMemberId, DateTimeOffset now)
    {
        EnsureMemberExists(ownerMemberId);

        var existing = _cards.SingleOrDefault(card => card.OwnerMemberId == ownerMemberId);
        if (existing is not null)
        {
            return existing;
        }

        var card = BankCard.Issue(ownerMemberId, _currencies, now);
        _cards.Add(card);
        return card;
    }

    public bool IsOwner(long telegramUserId) => OwnerTelegramUserId == telegramUserId;

    public void EnsureOwner(long telegramUserId)
    {
        if (!IsOwner(telegramUserId))
        {
            throw new DomainException("Начислять валюту может только владелец банка.");
        }
    }

    public BankTransaction CreditCard(Guid cardId, Money money, DateTimeOffset now, string? recipientDisplayName = null)
    {
        EnsureCurrencyExists(money.CurrencyCode);
        GetCard(cardId).Credit(money);
        var transaction = recipientDisplayName is null
            ? BankTransaction.CurrencyIssued(Id, cardId, money, now)
            : BankTransaction.CurrencyIssuedToMember(Id, cardId, money, recipientDisplayName, now);
        _transactions.Add(transaction);

        return transaction;
    }

    public BankTransaction BuyProduct(Guid buyerCardId, Guid productId, DateTimeOffset now)
    {
        var shop = Shop ?? throw new DomainException("Shop is not opened yet.");
        var product = shop.GetActiveProduct(productId);

        GetCard(buyerCardId).Debit(product.Price);
        var transaction = BankTransaction.Purchase(Id, buyerCardId, product.Price, product.Name, now);
        _transactions.Add(transaction);

        return transaction;
    }

    public Shop OpenShop(DateTimeOffset now)
    {
        Shop ??= new Shop(Id, now);
        return Shop;
    }

    public BankCard GetCard(Guid cardId) =>
        _cards.SingleOrDefault(card => card.Id == cardId) ?? throw new DomainException("Card was not found.");

    public BankMember GetMember(long telegramUserId) =>
        _members.SingleOrDefault(member => member.TelegramUserId == telegramUserId)
        ?? throw new DomainException("You are not a member of this bank.");

    public BankCard GetCardForMember(long telegramUserId)
    {
        var member = GetMember(telegramUserId);

        return _cards.SingleOrDefault(card => card.OwnerMemberId == member.Id)
            ?? throw new DomainException("Member does not have a card yet.");
    }

    private void EnsureMemberExists(Guid memberId)
    {
        if (_members.All(member => member.Id != memberId))
        {
            throw new DomainException("Bank member was not found.");
        }
    }

    private void EnsureCurrencyExists(string currencyCode)
    {
        if (_currencies.All(currency => !string.Equals(currency.Code, currencyCode, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainException($"Currency '{currencyCode}' is not available in this bank.");
        }
    }
}
