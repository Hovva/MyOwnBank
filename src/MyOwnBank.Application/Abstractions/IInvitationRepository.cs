namespace MyOwnBank.Application.Abstractions;

public interface IInvitationRepository
{
    Task AddAsync(BankInvitation invitation, CancellationToken cancellationToken);

    Task<BankInvitation?> GetActiveByCodeAsync(string code, DateTimeOffset now, CancellationToken cancellationToken);

    Task MarkUsedAsync(string code, long usedByTelegramUserId, DateTimeOffset usedAt, CancellationToken cancellationToken);
}

public sealed record BankInvitation(
    string Code,
    Guid BankId,
    long CreatedByTelegramUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    long? UsedByTelegramUserId,
    DateTimeOffset? UsedAt);
