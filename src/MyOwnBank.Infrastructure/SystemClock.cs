using MyOwnBank.Application.Common;

namespace MyOwnBank.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
