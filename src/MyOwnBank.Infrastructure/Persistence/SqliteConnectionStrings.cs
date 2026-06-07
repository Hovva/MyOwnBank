using Microsoft.Extensions.Configuration;

namespace MyOwnBank.Infrastructure.Persistence;

public static class SqliteConnectionStrings
{
    public const string DatabaseFileName = "my-own-bank.db";
    public const string ProductionMountPath = "/data";
    private const string SolutionFileName = "MyOwnBank.sln";

    public static string Resolve(IConfiguration configuration, bool isDevelopment)
    {
        if (isDevelopment)
        {
            var sharedPath = GetSharedDevelopmentDatabasePath();
            Directory.CreateDirectory(Path.GetDirectoryName(sharedPath)!);
            return $"Data Source={sharedPath}";
        }

        var configured = configuration.GetConnectionString("MyOwnBank");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return ToAbsolute(configured);
        }

        var mountPath = configuration["Persistence:MountPath"] ?? ProductionMountPath;
        Directory.CreateDirectory(mountPath);

        return $"Data Source={Path.Combine(mountPath, DatabaseFileName)}";
    }

    public static string GetSharedDevelopmentDatabasePath() =>
        Path.GetFullPath(Path.Combine(FindRepositoryRoot(), "data", DatabaseFileName));

    public static string ToAbsolute(string connectionString)
    {
        var path = TryExtractDataSource(connectionString);
        return path is null
            ? connectionString
            : $"Data Source={Path.GetFullPath(path)}";
    }

    public static string? TryExtractDataSource(string connectionString)
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

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not find repository root. Expected '{SolutionFileName}' above '{AppContext.BaseDirectory}'.");
    }
}
