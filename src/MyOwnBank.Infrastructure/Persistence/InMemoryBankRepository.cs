using System.Collections.Concurrent;
using MyOwnBank.Application.Abstractions;
using MyOwnBank.Domain.Banks;

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

    public Task<Bank?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken)
    {
        var bank = _banks.Values.FirstOrDefault(item =>
            item.Members.Any(member => member.TelegramUserId == telegramUserId));

        return Task.FromResult(bank);
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
