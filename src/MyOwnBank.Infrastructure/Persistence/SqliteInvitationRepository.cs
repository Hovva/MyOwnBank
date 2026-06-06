using Microsoft.EntityFrameworkCore;
using MyOwnBank.Application.Abstractions;
using MyOwnBank.Infrastructure.Persistence.Entities;

namespace MyOwnBank.Infrastructure.Persistence;

public sealed class SqliteInvitationRepository(IDbContextFactory<MyOwnBankDbContext> dbContextFactory) : IInvitationRepository
{
    public async Task AddAsync(BankInvitation invitation, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        db.Invitations.Add(ToEntity(invitation));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<BankInvitation?> GetActiveByCodeAsync(string code, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCode(code);
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Invitations
            .AsNoTracking()
            .SingleOrDefaultAsync(invitation => invitation.Code == normalizedCode, cancellationToken);

        if (entity is null || entity.UsedAt is not null || entity.ExpiresAt <= now)
        {
            return null;
        }

        return ToApplication(entity);
    }

    public async Task MarkUsedAsync(string code, long usedByTelegramUserId, DateTimeOffset usedAt, CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCode(code);
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await db.Invitations
            .Where(invitation => invitation.Code == normalizedCode)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(invitation => invitation.UsedByTelegramUserId, usedByTelegramUserId)
                .SetProperty(invitation => invitation.UsedAt, usedAt), cancellationToken);
    }

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    private static InvitationEntity ToEntity(BankInvitation invitation) =>
        new()
        {
            Code = NormalizeCode(invitation.Code),
            BankId = invitation.BankId,
            CreatedByTelegramUserId = invitation.CreatedByTelegramUserId,
            CreatedAt = invitation.CreatedAt,
            ExpiresAt = invitation.ExpiresAt,
            UsedByTelegramUserId = invitation.UsedByTelegramUserId,
            UsedAt = invitation.UsedAt
        };

    private static BankInvitation ToApplication(InvitationEntity entity) =>
        new(
            entity.Code,
            entity.BankId,
            entity.CreatedByTelegramUserId,
            entity.CreatedAt,
            entity.ExpiresAt,
            entity.UsedByTelegramUserId,
            entity.UsedAt);
}
