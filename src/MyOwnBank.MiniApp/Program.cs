using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyOwnBank.Application.Banks;
using MyOwnBank.Infrastructure;
using MyOwnBank.Infrastructure.Persistence;
using MyOwnBank.MiniApp;
using MyOwnBank.MiniApp.Options;
using MyOwnBank.MiniApp.Persistence;
using MyOwnBank.MiniApp.Storage;
using MyOwnBank.MiniApp.Telegram;

var builder = WebApplication.CreateBuilder(args);

var connectionString = PersistencePaths.ResolveDatabaseConnectionString(builder.Configuration, builder.Environment.IsDevelopment());
PersistencePaths.EnsureDatabaseDirectory(connectionString);

builder.Services
    .Configure<TelegramBotOptions>(builder.Configuration.GetSection(TelegramBotOptions.SectionName))
    .AddInfrastructure(connectionString)
    .AddSingleton<BankService>()
    .AddSingleton<TelegramInitDataValidator>()
    .AddSingleton<TelegramNotificationSender>()
    .AddSingleton<NotificationDeliveryService>()
    .AddSingleton<CardImageStorage>();

var app = builder.Build();

var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
Console.WriteLine();
Console.WriteLine();
Console.WriteLine("connection string для app 2 (Mini-App)");
Console.WriteLine(connectionString);
Console.WriteLine();
Console.WriteLine();
startupLogger.LogInformation("SQLite database: {ConnectionString}", connectionString);

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<MyOwnBankDbContext>>();
    await using var db = await dbContextFactory.CreateDbContextAsync();
    await db.Database.EnsureCreatedAsync();
    await DatabaseSchemaUpdater.ApplyAsync(db);
}

app.MapGet("/health", () => Results.Text("miniapp"));

app.UseDefaultFiles();
app.UseStaticFiles();

MiniAppApi.Map(app);

await app.RunAsync();
