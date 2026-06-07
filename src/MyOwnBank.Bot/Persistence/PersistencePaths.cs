using Microsoft.Extensions.Configuration;

namespace MyOwnBank.Bot.Persistence;

internal static class PersistencePaths
{
    public const string DefaultMountPath = "/data";
    public const string DatabaseFileName = "my-own-bank.db";

    public static string ResolveDatabaseConnectionString(IConfiguration configuration)
    {
        var configured = configuration.GetConnectionString("MyOwnBank");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var mountPath = configuration["Persistence:MountPath"] ?? DefaultMountPath;
        EnsureDirectory(mountPath);

        return $"Data Source={Path.Combine(mountPath, DatabaseFileName)}";
    }

    public static void EnsureDatabaseDirectory(string connectionString)
    {
        var databasePath = TryExtractSqlitePath(connectionString);
        if (databasePath is null)
        {
            return;
        }

        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            EnsureDirectory(directory);
        }
    }

    private static void EnsureDirectory(string path) =>
        Directory.CreateDirectory(path);

    private static string? TryExtractSqlitePath(string connectionString)
    {
        const string prefix = "Data Source=";
        var index = connectionString.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        var value = connectionString[(index + prefix.Length)..].Trim().Trim('"');
        var semicolonIndex = value.IndexOf(';');
        if (semicolonIndex >= 0)
        {
            value = value[..semicolonIndex];
        }

        return value;
    }
}
