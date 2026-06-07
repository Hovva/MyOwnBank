using MyOwnBank.Domain.Banks;
using MyOwnBank.Domain.Currencies;
using MyOwnBank.Domain.Shops;
using MyOwnBank.Domain.Transactions;
using MyOwnBank.Infrastructure.Persistence.Entities;

namespace MyOwnBank.Infrastructure.Persistence;

internal static class BankMapper
{
    public static Bank ToDomain(BankEntity entity)
    {
        var currencies = entity.Currencies.Select(item =>
            new Currency(item.Code, item.Name, item.Icon));
        var members = entity.Members.Select(item =>
            BankMember.Rehydrate(item.Id, item.TelegramUserId, item.DisplayName, item.JoinedAt));
        var cards = entity.Cards.Select(item =>
            BankCard.Rehydrate(
                item.Id,
                item.OwnerMemberId,
                item.CardNumber,
                item.HolderName,
                item.Balances.ToDictionary(balance => balance.CurrencyCode, balance => balance.Amount),
                item.IssuedAt));
        var shop = entity.Shop is null
            ? null
            : Shop.Rehydrate(
                entity.Id,
                entity.Shop.OpenedAt,
                entity.Shop.Products.Select(product => ShopProduct.Rehydrate(
                    product.Id,
                    product.Name,
                    product.Description,
                    new Money(product.CurrencyCode, product.Price),
                    product.IsActive,
                    product.CreatedAt)));
        var transactions = entity.Transactions.Select(item =>
            BankTransaction.Rehydrate(
                item.Id,
                item.BankId,
                item.CardId,
                item.Type,
                item.CurrencyCode,
                item.Amount,
                item.Description,
                item.OccurredAt));

        return Bank.Rehydrate(
            entity.Id,
            entity.Name,
            entity.OwnerTelegramUserId,
            entity.CreatedAt,
            currencies,
            members,
            cards,
            shop,
            transactions);
    }

    public static BankEntity ToEntity(Bank bank) =>
        new()
        {
            Id = bank.Id,
            Name = bank.Name,
            OwnerTelegramUserId = bank.OwnerTelegramUserId,
            CreatedAt = bank.CreatedAt,
            Currencies = bank.Currencies.Select(currency => new BankCurrencyEntity
            {
                BankId = bank.Id,
                Code = currency.Code,
                Name = currency.Name,
                Icon = currency.ResolveIcon()
            }).ToArray(),
            Members = bank.Members.Select(member => new BankMemberEntity
            {
                Id = member.Id,
                BankId = bank.Id,
                TelegramUserId = member.TelegramUserId,
                DisplayName = member.DisplayName,
                JoinedAt = member.JoinedAt
            }).ToArray(),
            Cards = bank.Cards.Select(card => new BankCardEntity
            {
                Id = card.Id,
                BankId = bank.Id,
                OwnerMemberId = card.OwnerMemberId,
                CardNumber = card.CardNumber,
                HolderName = card.HolderName,
                IssuedAt = card.IssuedAt,
                Balances = card.Balances.Select(balance => new CardBalanceEntity
                {
                    CardId = card.Id,
                    CurrencyCode = balance.Key,
                    Amount = balance.Value
                }).ToArray()
            }).ToArray(),
            Shop = bank.Shop is null ? null : new ShopEntity
            {
                BankId = bank.Id,
                OpenedAt = bank.Shop.OpenedAt,
                Products = bank.Shop.Products.Select(product => new ShopProductEntity
                {
                    Id = product.Id,
                    BankId = bank.Id,
                    Name = product.Name,
                    Description = product.Description,
                    CurrencyCode = product.Price.CurrencyCode,
                    Price = product.Price.Amount,
                    IsActive = product.IsActive,
                    CreatedAt = product.CreatedAt
                }).ToArray()
            },
            Transactions = bank.Transactions.Select(transaction => new BankTransactionEntity
            {
                Id = transaction.Id,
                BankId = bank.Id,
                CardId = transaction.CardId,
                Type = transaction.Type,
                CurrencyCode = transaction.CurrencyCode,
                Amount = transaction.Amount,
                Description = transaction.Description,
                OccurredAt = transaction.OccurredAt
            }).ToArray()
        };
}
