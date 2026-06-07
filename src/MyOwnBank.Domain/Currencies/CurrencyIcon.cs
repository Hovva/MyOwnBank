using MyOwnBank.Domain.Common;

namespace MyOwnBank.Domain.Currencies;

public static class CurrencyIcon
{
    public const string ImagePrefix = "/currency-icons/";

    public const int MaxEmojiLength = 8;

    public const int MaxImageUrlLength = 128;

    public static bool IsImage(string? icon) =>
        !string.IsNullOrWhiteSpace(icon) && icon.Trim().StartsWith(ImagePrefix, StringComparison.Ordinal);

    public static string Normalize(string icon)
    {
        var normalized = icon.Trim();
        if (normalized.Length == 0)
        {
            throw new DomainException("Иконка не может быть пустой.");
        }

        if (IsImage(normalized))
        {
            if (normalized.Length > MaxImageUrlLength)
            {
                throw new DomainException("Ссылка на иконку слишком длинная.");
            }

            if (normalized.Contains('\n') || normalized.Contains('\r'))
            {
                throw new DomainException("Иконка должна быть эмодзи или картинкой.");
            }

            return normalized;
        }

        if (normalized.Length > MaxEmojiLength)
        {
            throw new DomainException("Эмодзи слишком длинное.");
        }

        if (normalized.Contains('\n') || normalized.Contains('\r'))
        {
            throw new DomainException("Иконка должна быть эмодзи или картинкой.");
        }

        return normalized;
    }
}
