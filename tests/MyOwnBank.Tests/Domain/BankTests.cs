using MyOwnBank.Domain.Banks;
using MyOwnBank.Domain.Common;
using MyOwnBank.Domain.Currencies;

namespace MyOwnBank.Tests.Domain;

public sealed class BankTests
{
    private static readonly Currency[] SampleCurrencies =
    [
        Currency.Hug,
        Currency.Kiss,
        Currency.Spank
    ];

    [Fact]
    public void Create_IssuesOwnerCardWithProvidedCurrencies()
    {
        var bank = Bank.Create("My own bank", 123, "Owner", SampleCurrencies, DateTimeOffset.UtcNow);

        var card = Assert.Single(bank.Cards);

        Assert.Equal(3, card.Balances.Count);
        Assert.Contains("hug", card.Balances.Keys);
        Assert.Contains("kiss", card.Balances.Keys);
        Assert.Contains("spank", card.Balances.Keys);
    }

    [Fact]
    public void Create_RequiresAtLeastOneCurrency()
    {
        Assert.Throws<DomainException>(() =>
            Bank.Create("My own bank", 123, "Owner", Array.Empty<Currency>(), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void BuyProduct_DebitsBuyerCard()
    {
        var bank = Bank.Create("My own bank", 123, "Owner", SampleCurrencies, DateTimeOffset.UtcNow);
        var card = bank.Cards.Single();
        var shop = bank.OpenShop(DateTimeOffset.UtcNow);
        var product = shop.AddProduct("Real reward", new("kiss", 3), DateTimeOffset.UtcNow);

        bank.CreditCard(card.Id, new("kiss", 5), DateTimeOffset.UtcNow);
        bank.BuyProduct(card.Id, product.Id, DateTimeOffset.UtcNow, "alice");

        Assert.Equal(2, card.Balances["kiss"]);
    }

    [Fact]
    public void CreditCard_RecordsCurrencyIssuedTransaction()
    {
        var now = DateTimeOffset.UtcNow;
        var bank = Bank.Create("My own bank", 123, "Owner", SampleCurrencies, now);
        var card = bank.Cards.Single();

        bank.CreditCard(card.Id, new("hug", 5), now);

        var transaction = Assert.Single(bank.Transactions);
        Assert.Equal(card.Id, transaction.CardId);
        Assert.Equal("currency-issued", transaction.Type);
        Assert.Equal("hug", transaction.CurrencyCode);
        Assert.Equal(5, transaction.Amount);
    }

    [Fact]
    public void BuyProduct_RecordsPurchaseTransaction()
    {
        var now = DateTimeOffset.UtcNow;
        var bank = Bank.Create("My own bank", 123, "Owner", SampleCurrencies, now);
        var card = bank.Cards.Single();
        var shop = bank.OpenShop(now);
        var product = shop.AddProduct("Real kiss", new("kiss", 3), now);

        bank.CreditCard(card.Id, new("kiss", 5), now);
        bank.BuyProduct(card.Id, product.Id, now, "Owner");

        var transaction = bank.Transactions.Last();
        Assert.Equal(card.Id, transaction.CardId);
        Assert.Equal("purchase", transaction.Type);
        Assert.Equal("kiss", transaction.CurrencyCode);
        Assert.Equal(-3, transaction.Amount);
        Assert.Contains("Real kiss", transaction.Description);
    }

    [Fact]
    public void CreditCard_OnlyOwnerCanIssueCurrency()
    {
        var now = DateTimeOffset.UtcNow;
        var bank = Bank.Create("My own bank", 123, "Owner", SampleCurrencies, now);
        bank.AddMember(456, "Partner", now);

        bank.EnsureOwner(123);
        Assert.Throws<DomainException>(() => bank.EnsureOwner(456));
    }

    [Fact]
    public void TryResolveCode_AcceptsRussianCurrencyNames()
    {
        Assert.True(Currency.TryResolveCode("обнимашки", out var hug));
        Assert.Equal("hug", hug);

        Assert.True(Currency.TryResolveCode("поцелуйчики", out var kiss));
        Assert.Equal("kiss", kiss);

        Assert.True(Currency.TryResolveCode("порка", out var spank));
        Assert.Equal("spank", spank);
    }

    [Fact]
    public void BuyProduct_FailsWhenBalanceIsNotEnough()
    {
        var bank = Bank.Create("My own bank", 123, "Owner", SampleCurrencies, DateTimeOffset.UtcNow);
        var card = bank.Cards.Single();
        var shop = bank.OpenShop(DateTimeOffset.UtcNow);
        var product = shop.AddProduct("Real reward", new("kiss", 3), DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(() => bank.BuyProduct(card.Id, product.Id, DateTimeOffset.UtcNow, "alice"));
    }
}
