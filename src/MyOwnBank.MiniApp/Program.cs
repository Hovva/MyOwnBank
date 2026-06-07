using Microsoft.EntityFrameworkCore;
using MyOwnBank.Application.Banks;
using MyOwnBank.Infrastructure;
using MyOwnBank.Infrastructure.Persistence;
using MyOwnBank.MiniApp.Options;
using MyOwnBank.MiniApp.Persistence;
using MyOwnBank.MiniApp.Telegram;

var builder = WebApplication.CreateBuilder(args);

var connectionString = PersistencePaths.ResolveDatabaseConnectionString(builder.Configuration);
PersistencePaths.EnsureDatabaseDirectory(connectionString);

builder.Services
    .Configure<TelegramBotOptions>(builder.Configuration.GetSection(TelegramBotOptions.SectionName))
    .AddInfrastructure(connectionString)
    .AddSingleton<BankService>()
    .AddSingleton<TelegramInitDataValidator>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<MyOwnBankDbContext>>();
    await using var db = await dbContextFactory.CreateDbContextAsync();
    await db.Database.EnsureCreatedAsync();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/dashboard", async (MiniAppRequest request, BankService bankService, TelegramInitDataValidator validator, CancellationToken cancellationToken) =>
{
    if (!TryGetUser(request.InitData, validator, out var userId, out var displayName))
    {
        return Results.Unauthorized();
    }

    var bank = await bankService.GetMyBankAsync(userId, cancellationToken);
    if (bank is null)
    {
        return Results.Ok(new MiniAppDashboardResponse(
            userId,
            displayName,
            HasBank: false,
            BankName: null,
            IsOwner: false,
            MemberCount: 0,
            Balances: null,
            Products: [],
            Transactions: []));
    }

    var member = bank.Members.Single(item => item.TelegramUserId == userId);
    var card = bank.Cards.Single(item => item.OwnerMemberId == member.Id);
    var history = await bankService.GetHistoryAsync(userId, cancellationToken);

    return Results.Ok(new MiniAppDashboardResponse(
        userId,
        displayName,
        HasBank: true,
        bank.Name,
        bank.OwnerTelegramUserId == userId,
        bank.Members.Count,
        card.Balances,
        bank.Products.Where(product => product.IsActive).ToArray(),
        history.ToArray()));
});

app.MapPost("/api/command", async (MiniAppCommandRequest request, BankService bankService, TelegramInitDataValidator validator, CancellationToken cancellationToken) =>
{
    if (!TryGetUser(request.InitData, validator, out var userId, out var displayName))
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(request.Command))
    {
        return Results.BadRequest(new MiniAppCommandResponse("Введите команду."));
    }

    var response = await ExecuteCommandAsync(request.Command.Trim(), userId, displayName, bankService, cancellationToken);
    return Results.Ok(new MiniAppCommandResponse(response));
});

await app.RunAsync();

static bool TryGetUser(string initData, TelegramInitDataValidator validator, out long userId, out string displayName)
{
    if (validator.TryValidate(initData, out userId, out displayName))
    {
        return true;
    }

    userId = 0;
    displayName = string.Empty;
    return false;
}

static async Task<string> ExecuteCommandAsync(
    string command,
    long userId,
    string displayName,
    BankService bankService,
    CancellationToken cancellationToken)
{
    var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    var commandName = parts.FirstOrDefault()?.ToLowerInvariant();

    try
    {
        if (commandName is "/start" or "/balance")
        {
            var bank = await bankService.GetMyBankAsync(userId, cancellationToken);
            if (bank is null)
            {
                return "У тебя пока нет банка. Создай через /create <название> или вступи через /join <код>.";
            }

            var member = bank.Members.Single(item => item.TelegramUserId == userId);
            var card = bank.Cards.Single(item => item.OwnerMemberId == member.Id);
            var balances = string.Join(", ", card.Balances.Select(pair => $"{pair.Key}={pair.Value}"));
            return commandName == "/balance"
                ? $"Твой баланс:\n{balances}"
                : $"Банк: {bank.Name}\nУчастников: {bank.Members.Count}\nТвой баланс: {balances}";
        }

        if (commandName is "/create" or "/newbank")
        {
            var bankName = command[(commandName == "/create" ? "/create" : "/newbank").Length..].Trim();
            if (string.IsNullOrWhiteSpace(bankName))
            {
                bankName = $"{displayName}'s bank";
            }

            var bank = await bankService.CreateBankAsync(new CreateBankCommand(bankName, userId, displayName), cancellationToken);
            return $"Банк '{bank.Name}' готов.";
        }

        if (commandName == "/shop")
        {
            var products = await bankService.GetShopAsync(userId, cancellationToken);
            var active = products.Where(product => product.IsActive).ToArray();
            return active.Length == 0
                ? "Магазин пуст."
                : string.Join('\n', active.Select(product => $"{product.Id} - {product.Name}: {product.Price} {product.CurrencyCode}"));
        }

        if (commandName == "/history")
        {
            var history = await bankService.GetHistoryAsync(userId, cancellationToken);
            return history.Count == 0
                ? "Операций пока нет."
                : string.Join('\n', history.Select(item =>
                    $"{item.OccurredAt:MM-dd HH:mm}: {item.Amount:+0.##;-0.##} {item.CurrencyCode} - {item.Description}"));
        }

        return "Команды: /start, /balance, /create, /shop, /history";
    }
    catch (Exception ex)
    {
        return ex.Message;
    }
}

internal sealed record MiniAppRequest(string InitData);

internal sealed record MiniAppDashboardResponse(
    long UserId,
    string DisplayName,
    bool HasBank,
    string? BankName,
    bool IsOwner,
    int MemberCount,
    IReadOnlyDictionary<string, decimal>? Balances,
    IReadOnlyCollection<ProductSummary> Products,
    IReadOnlyCollection<TransactionSummary> Transactions);

internal sealed record MiniAppCommandRequest(string InitData, string Command);

internal sealed record MiniAppCommandResponse(string Response);
