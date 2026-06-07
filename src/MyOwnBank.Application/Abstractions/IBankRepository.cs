using MyOwnBank.Domain.Banks;
using MyOwnBank.Domain.Transactions;

namespace MyOwnBank.Application.Abstractions;

public interface IBankRepository
{
    Task AddAsync(Bank bank, CancellationToken cancellationToken);

    Task<Bank?> GetByIdAsync(Guid bankId, CancellationToken cancellationToken);

    Task<Bank?> GetByIdLiteAsync(Guid bankId, CancellationToken cancellationToken);

    Task<Bank?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken);

    Task<Bank?> GetByTelegramUserIdLiteAsync(long telegramUserId, CancellationToken cancellationToken);

    Task<(IReadOnlyList<BankTransaction> Transactions, bool HasMore)> GetCardTransactionsPageAsync(
        Guid bankId,
        Guid cardId,
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task SaveAsync(Bank bank, CancellationToken cancellationToken);

    Task DeleteAsync(Guid bankId, CancellationToken cancellationToken);
}
