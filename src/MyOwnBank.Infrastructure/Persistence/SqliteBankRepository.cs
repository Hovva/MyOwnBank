using Microsoft.EntityFrameworkCore;
using MyOwnBank.Application.Abstractions;
using MyOwnBank.Domain.Banks;
using MyOwnBank.Domain.Transactions;

namespace MyOwnBank.Infrastructure.Persistence;

public sealed class SqliteBankRepository(IDbContextFactory<MyOwnBankDbContext> dbContextFactory) : IBankRepository
{
    public async Task AddAsync(Bank bank, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        db.Banks.Add(BankMapper.ToEntity(bank));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Bank?> GetByIdAsync(Guid bankId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await QueryFull(db)
            .SingleOrDefaultAsync(bank => bank.Id == bankId, cancellationToken);

        return entity is null ? null : BankMapper.ToDomain(entity);
    }

    public async Task<Bank?> GetByIdLiteAsync(Guid bankId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await QueryLite(db)
            .SingleOrDefaultAsync(bank => bank.Id == bankId, cancellationToken);

        return entity is null ? null : BankMapper.ToDomain(entity);
    }

    public async Task<Bank?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await QueryFull(db)
            .SingleOrDefaultAsync(bank => bank.Members.Any(member => member.TelegramUserId == telegramUserId), cancellationToken);

        return entity is null ? null : BankMapper.ToDomain(entity);
    }

    public async Task<Bank?> GetByTelegramUserIdLiteAsync(long telegramUserId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await QueryLite(db)
            .SingleOrDefaultAsync(bank => bank.Members.Any(member => member.TelegramUserId == telegramUserId), cancellationToken);

        return entity is null ? null : BankMapper.ToDomain(entity);
    }

    public async Task<(IReadOnlyList<BankTransaction> Transactions, bool HasMore)> GetCardTransactionsPageAsync(
        Guid bankId,
        Guid cardId,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        if (take <= 0)
        {
            return ([], false);
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var filtered = await db.BankTransactions
            .AsNoTracking()
            .Where(transaction => transaction.BankId == bankId && transaction.CardId == cardId)
            .ToListAsync(cancellationToken);

        var entities = filtered
            .OrderByDescending(transaction => transaction.OccurredAt)
            .Skip(skip)
            .Take(take + 1)
            .ToList();

        var hasMore = entities.Count > take;
        if (hasMore)
        {
            entities.RemoveAt(entities.Count - 1);
        }

        var transactions = entities
            .Select(item => BankTransaction.Rehydrate(
                item.Id,
                item.BankId,
                item.CardId,
                item.Type,
                item.CurrencyCode,
                item.Amount,
                item.Description,
                item.OccurredAt))
            .ToArray();

        return (transactions, hasMore);
    }

    public async Task<(IReadOnlyList<BankTransaction> Transactions, bool HasMore)> GetBankTransactionsByTypePageAsync(
        Guid bankId,
        string type,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        if (take <= 0)
        {
            return ([], false);
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var filtered = await db.BankTransactions
            .AsNoTracking()
            .Where(transaction => transaction.BankId == bankId && transaction.Type == type)
            .ToListAsync(cancellationToken);

        var entities = filtered
            .OrderByDescending(transaction => transaction.OccurredAt)
            .Skip(skip)
            .Take(take + 1)
            .ToList();

        var hasMore = entities.Count > take;
        if (hasMore)
        {
            entities.RemoveAt(entities.Count - 1);
        }

        var transactions = entities
            .Select(item => BankTransaction.Rehydrate(
                item.Id,
                item.BankId,
                item.CardId,
                item.Type,
                item.CurrencyCode,
                item.Amount,
                item.Description,
                item.OccurredAt))
            .ToArray();

        return (transactions, hasMore);
    }

    public async Task SaveAsync(Bank bank, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await db.Banks
            .Where(entity => entity.Id == bank.Id)
            .ExecuteDeleteAsync(cancellationToken);

        db.Banks.Add(BankMapper.ToEntity(bank));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid bankId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;", cancellationToken);
        await db.Invitations
            .Where(invitation => invitation.BankId == bankId)
            .ExecuteDeleteAsync(cancellationToken);

        var bank = await db.Banks
            .Include(item => item.Currencies)
            .Include(item => item.Members)
            .Include(item => item.Cards)
            .ThenInclude(card => card.Balances)
            .Include(item => item.Shop)
            .ThenInclude(shop => shop!.Products)
            .Include(item => item.Transactions)
            .SingleOrDefaultAsync(item => item.Id == bankId, cancellationToken);

        if (bank is null)
        {
            return;
        }

        db.Banks.Remove(bank);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<Entities.BankEntity> QueryLite(MyOwnBankDbContext db) =>
        db.Banks
            .AsNoTracking()
            .Include(bank => bank.Currencies)
            .Include(bank => bank.Members)
            .Include(bank => bank.Cards)
            .ThenInclude(card => card.Balances)
            .Include(bank => bank.Shop)
            .ThenInclude(shop => shop!.Products);

    private static IQueryable<Entities.BankEntity> QueryFull(MyOwnBankDbContext db) =>
        QueryLite(db).Include(bank => bank.Transactions);
}
