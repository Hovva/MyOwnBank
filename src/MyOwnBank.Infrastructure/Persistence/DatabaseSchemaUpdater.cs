using Microsoft.EntityFrameworkCore;
using MyOwnBank.Domain.Banks;
using MyOwnBank.Domain.Currencies;
using MyOwnBank.Infrastructure.Persistence.Entities;

namespace MyOwnBank.Infrastructure.Persistence;

public static class DatabaseSchemaUpdater
{
    public static async Task ApplyAsync(MyOwnBankDbContext dbContext, CancellationToken cancellationToken = default)
    {
        await EnsureColumnAsync(dbContext, "bank_cards", "CardNumber", "TEXT NOT NULL DEFAULT ''", cancellationToken);
        await EnsureColumnAsync(dbContext, "bank_cards", "HolderName", "TEXT NULL", cancellationToken);
        await MigrateLegacyColumnDataAsync(dbContext, "bank_cards", "card_number", "CardNumber", cancellationToken);
        await MigrateLegacyColumnDataAsync(dbContext, "bank_cards", "holder_name", "HolderName", cancellationToken);
        await BackfillCardNumbersAsync(dbContext, cancellationToken);

        await EnsureColumnAsync(dbContext, "bank_currencies", "Icon", "TEXT NOT NULL DEFAULT ''", cancellationToken);
        await MigrateLegacyColumnDataAsync(dbContext, "bank_currencies", "icon", "Icon", cancellationToken);
        await BackfillCurrencyIconsAsync(dbContext, cancellationToken);

        await EnsureColumnAsync(dbContext, "shop_products", "Description", "TEXT NULL", cancellationToken);
        await EnsureUserNotificationsTableAsync(dbContext, cancellationToken);
    }

    private static async Task EnsureUserNotificationsTableAsync(
        MyOwnBankDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS user_notifications (
                Id TEXT NOT NULL PRIMARY KEY,
                RecipientTelegramUserId INTEGER NOT NULL,
                BankId TEXT NOT NULL,
                Type TEXT NOT NULL,
                Title TEXT NOT NULL,
                Message TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                IsRead INTEGER NOT NULL DEFAULT 0
            );
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS IX_user_notifications_RecipientTelegramUserId_CreatedAt
            ON user_notifications (RecipientTelegramUserId, CreatedAt);
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS IX_user_notifications_RecipientTelegramUserId_IsRead
            ON user_notifications (RecipientTelegramUserId, IsRead);
            """,
            cancellationToken);
    }

    private static async Task EnsureColumnAsync(
        MyOwnBankDbContext dbContext,
        string tableName,
        string columnName,
        string definition,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);

        try
        {
            await using var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = $"PRAGMA table_info({tableName})";

            var hasColumn = false;
            await using (var reader = await checkCommand.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var name = reader.GetString(1);
                    if (string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase))
                    {
                        hasColumn = true;
                        break;
                    }
                }
            }

            if (hasColumn)
            {
                return;
            }

            await using var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {definition}";
            await alterCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private static async Task MigrateLegacyColumnDataAsync(
        MyOwnBankDbContext dbContext,
        string tableName,
        string legacyColumnName,
        string targetColumnName,
        CancellationToken cancellationToken)
    {
        if (string.Equals(legacyColumnName, targetColumnName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);

        try
        {
            var columns = await GetColumnNamesAsync(connection, tableName, cancellationToken);
            if (!columns.Contains(legacyColumnName, StringComparer.OrdinalIgnoreCase)
                || !columns.Contains(targetColumnName, StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            await using var migrateCommand = connection.CreateCommand();
            migrateCommand.CommandText =
                $"UPDATE {tableName} SET {targetColumnName} = {legacyColumnName} " +
                $"WHERE ({targetColumnName} IS NULL OR {targetColumnName} = '') " +
                $"AND ({legacyColumnName} IS NOT NULL AND {legacyColumnName} <> '')";
            await migrateCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private static async Task<List<string>> GetColumnNamesAsync(
        System.Data.Common.DbConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = $"PRAGMA table_info({tableName})";

        var columns = new List<string>();
        await using var reader = await checkCommand.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }

    private static async Task BackfillCardNumbersAsync(MyOwnBankDbContext dbContext, CancellationToken cancellationToken)
    {
        var cards = await dbContext.Set<BankCardEntity>()
            .Where(card => card.CardNumber == null || card.CardNumber == "")
            .ToListAsync(cancellationToken);

        if (cards.Count == 0)
        {
            return;
        }

        foreach (var card in cards)
        {
            card.CardNumber = CardNumberGenerator.Generate();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task BackfillCurrencyIconsAsync(MyOwnBankDbContext dbContext, CancellationToken cancellationToken)
    {
        var currencies = await dbContext.Set<BankCurrencyEntity>()
            .Where(currency => currency.Icon == null || currency.Icon == "")
            .ToListAsync(cancellationToken);

        if (currencies.Count == 0)
        {
            return;
        }

        foreach (var currency in currencies)
        {
            currency.Icon = Currency.ResolveDefaultIcon(currency.Code);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
