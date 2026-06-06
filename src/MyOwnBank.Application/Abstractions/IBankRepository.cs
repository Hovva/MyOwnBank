using MyOwnBank.Domain.Banks;

namespace MyOwnBank.Application.Abstractions;

public interface IBankRepository
{
    Task AddAsync(Bank bank, CancellationToken cancellationToken);

    Task<Bank?> GetByIdAsync(Guid bankId, CancellationToken cancellationToken);

    Task<Bank?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken);

    Task SaveAsync(Bank bank, CancellationToken cancellationToken);
}
