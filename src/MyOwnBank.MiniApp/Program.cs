using Microsoft.EntityFrameworkCore;
using MyOwnBank.Application.Banks;
using MyOwnBank.Infrastructure;
using MyOwnBank.Infrastructure.Persistence;
using MyOwnBank.MiniApp;
using MyOwnBank.MiniApp.Options;
using MyOwnBank.MiniApp.Persistence;
using MyOwnBank.MiniApp.Storage;
using MyOwnBank.MiniApp.Telegram;

var builder = WebApplication.CreateBuilder(args);

var connectionString = PersistencePaths.ResolveDatabaseConnectionString(builder.Configuration);
PersistencePaths.EnsureDatabaseDirectory(connectionString);

builder.Services
    .Configure<TelegramBotOptions>(builder.Configuration.GetSection(TelegramBotOptions.SectionName))
    .AddInfrastructure(connectionString)
    .AddSingleton<BankService>()
    .AddSingleton<TelegramInitDataValidator>()
    .AddSingleton<CardImageStorage>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<MyOwnBankDbContext>>();
    await using var db = await dbContextFactory.CreateDbContextAsync();
    await db.Database.EnsureCreatedAsync();
}

app.UseDefaultFiles();
app.UseStaticFiles();

MiniAppApi.Map(app);

await app.RunAsync();
