namespace MyOwnBank.Application.Common;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
