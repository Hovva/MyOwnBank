using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using MyOwnBank.MiniApp.Options;

namespace MyOwnBank.MiniApp.Telegram;

public sealed class TelegramInitDataValidator(IOptions<TelegramBotOptions> options)
{
    public bool TryValidate(string initData, out long userId, out string displayName)
    {
        userId = 0;
        displayName = string.Empty;

        if (string.IsNullOrWhiteSpace(initData) || string.IsNullOrWhiteSpace(options.Value.Token))
        {
            return false;
        }

        var values = ParseQueryString(initData);
        if (!values.TryGetValue("hash", out var receivedHash))
        {
            return false;
        }

        var dataCheckString = string.Join(
            '\n',
            values
                .Where(pair => pair.Key != "hash")
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key}={pair.Value}"));

        var secretKey = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes("WebAppData"),
            Encoding.UTF8.GetBytes(options.Value.Token));

        var calculatedHash = Convert.ToHexString(
                HMACSHA256.HashData(secretKey, Encoding.UTF8.GetBytes(dataCheckString)))
            .ToLowerInvariant();

        if (!string.Equals(receivedHash, calculatedHash, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!values.TryGetValue("user", out var userJson))
        {
            return false;
        }

        using var document = System.Text.Json.JsonDocument.Parse(userJson);
        var root = document.RootElement;
        if (!root.TryGetProperty("id", out var idProperty))
        {
            return false;
        }

        userId = idProperty.GetInt64();
        displayName = root.TryGetProperty("username", out var usernameProperty) && usernameProperty.GetString() is { Length: > 0 } username
            ? username
            : root.TryGetProperty("first_name", out var firstNameProperty) && firstNameProperty.GetString() is { Length: > 0 } firstName
                ? firstName
                : userId.ToString();

        return true;
    }

    private static Dictionary<string, string> ParseQueryString(string initData)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in initData.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = part.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(part[..separatorIndex]);
            var value = Uri.UnescapeDataString(part[(separatorIndex + 1)..]);
            result[key] = value;
        }

        return result;
    }
}
