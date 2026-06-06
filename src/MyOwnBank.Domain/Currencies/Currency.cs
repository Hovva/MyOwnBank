namespace MyOwnBank.Domain.Currencies;

public sealed record Currency(string Code, string Name)
{
    public static Currency Hug { get; } = new("hug", "обнимашки");
    public static Currency Kiss { get; } = new("kiss", "поцелуйчики");
    public static Currency Spank { get; } = new("spank", "порка");

    public static IReadOnlyCollection<Currency> DefaultCurrencies { get; } =
    [
        Hug,
        Kiss,
        Spank
    ];

    public static bool TryResolveCode(string input, out string code)
    {
        var normalized = input.Trim();
        foreach (var currency in DefaultCurrencies)
        {
            if (string.Equals(currency.Code, normalized, StringComparison.OrdinalIgnoreCase)
                || string.Equals(currency.Name, normalized, StringComparison.OrdinalIgnoreCase))
            {
                code = currency.Code;
                return true;
            }
        }

        code = normalized.ToLowerInvariant() switch
        {
            "поцелуй" or "поцелуи" or "поцелуев" => Kiss.Code,
            _ => string.Empty
        };

        return code.Length > 0;
    }
}
