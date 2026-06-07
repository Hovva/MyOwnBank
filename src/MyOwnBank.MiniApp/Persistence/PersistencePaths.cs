using Microsoft.Extensions.Configuration;
using MyOwnBank.Infrastructure.Persistence;

namespace MyOwnBank.MiniApp.Persistence;

internal static class PersistencePaths
{
    public const string DefaultMountPath = SqliteConnectionStrings.ProductionMountPath;
    public const string DatabaseFileName = SqliteConnectionStrings.DatabaseFileName;

    public static string ResolveDatabaseConnectionString(IConfiguration configuration, bool isDevelopment) =>
        SqliteConnectionStrings.Resolve(configuration, isDevelopment);

    public static void EnsureDatabaseDirectory(string connectionString)
    {
        var databasePath = SqliteConnectionStrings.TryExtractDataSource(connectionString);
        if (databasePath is null)
        {
            return;
        }

        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
