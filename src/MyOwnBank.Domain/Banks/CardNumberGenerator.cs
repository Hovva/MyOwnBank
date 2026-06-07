namespace MyOwnBank.Domain.Banks;

public static class CardNumberGenerator
{
    public static string Generate() =>
        $"{Random.Shared.Next(4000, 4999)} {Random.Shared.Next(1000, 9999)} {Random.Shared.Next(1000, 9999)} {Random.Shared.Next(1000, 9999)}";
}
