using System.Collections.Concurrent;
using MyOwnBank.Application.Abstractions;
using MyOwnBank.Domain.Banks;
using MyOwnBank.Domain.Transactions;

namespace MyOwnBank.Infrastructure.Persistence;

public sealed class InMemoryBankRepository : IBankRepository
{
    private readonly ConcurrentDictionary<Guid, Bank> _banks = [];

    public Task AddAsync(Bank bank, CancellationToken cancellationToken)
    {
        _banks.TryAdd(bank.Id, bank);
        return Task.CompletedTask;
    }

    public Task<Bank?> GetByIdAsync(Guid bankId, CancellationToken cancellationToken)
    {
        _banks.TryGetValue(bankId, out var bank);
        return Task.FromResult(bank);
    }

    public Task<Bank?> GetByIdLiteAsync(Guid bankId, CancellationToken cancellationToken) =>
        GetByIdAsync(bankId, cancellationToken);

    public Task<Bank?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken)
    {
        var bank = _banks.Values.FirstOrDefault(item =>
            item.Members.Any(member => member.TelegramUserId == telegramUserId));

        return Task.FromResult(bank);
    }

    public Task<Bank?> GetByTelegramUserIdLiteAsync(long telegramUserId, CancellationToken cancellationToken) =>
        GetByTelegramUserIdAsync(telegramUserId, cancellationToken);

    public Task<(IReadOnlyList<BankTransaction> Transactions, bool HasMore)> GetCardTransactionsPageAsync(
        Guid bankId,
        Guid cardId,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        if (!_banks.TryGetValue(bankId, out var bank) || take <= 0)
        {
            return Task.FromResult(((IReadOnlyList<BankTransaction>)[], false));
        }

        var transactions = bank.Transactions
            .Where(transaction => transaction.CardId == cardId)
            .OrderByDescending(transaction => transaction.OccurredAt)
            .Skip(skip)
            .Take(take + 1)
            .ToList();

        var hasMore = transactions.Count > take;
        if (hasMore)
        {
            transactions.RemoveAt(transactions.Count - 1);
        }

        return Task.FromResult(((IReadOnlyList<BankTransaction>)transactions, hasMore));
    }

    public Task SaveAsync(Bank bank, CancellationToken cancellationToken)
    {
        _banks[bank.Id] = bank;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid bankId, CancellationToken cancellationToken)
    {
        _banks.TryRemove(bankId, out _);
        return Task.CompletedTask;
    }
}
