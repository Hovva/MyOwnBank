using Microsoft.EntityFrameworkCore;
using MyOwnBank.Application.Abstractions;
using MyOwnBank.Domain.Banks;

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
        var entity = await Query(db)
            .SingleOrDefaultAsync(bank => bank.Id == bankId, cancellationToken);

        return entity is null ? null : BankMapper.ToDomain(entity);
    }

    public async Task<Bank?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await Query(db)
            .SingleOrDefaultAsync(bank => bank.Members.Any(member => member.TelegramUserId == telegramUserId), cancellationToken);

        return entity is null ? null : BankMapper.ToDomain(entity);
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

    private static IQueryable<Entities.BankEntity> Query(MyOwnBankDbContext db) =>
        db.Banks
            .AsNoTracking()
            .Include(bank => bank.Currencies)
            .Include(bank => bank.Members)
            .Include(bank => bank.Cards)
            .ThenInclude(card => card.Balances)
            .Include(bank => bank.Shop)
            .ThenInclude(shop => shop!.Products)
            .Include(bank => bank.Transactions);
}
