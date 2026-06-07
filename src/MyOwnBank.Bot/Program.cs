using System.Net.Mime;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyOwnBank.Application.Banks;
using MyOwnBank.Bot.Options;
using MyOwnBank.Bot.Persistence;
using MyOwnBank.Bot.Telegram;
using MyOwnBank.Infrastructure;
using MyOwnBank.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString = PersistencePaths.ResolveDatabaseConnectionString(builder.Configuration, builder.Environment.IsDevelopment());
PersistencePaths.EnsureDatabaseDirectory(connectionString);

builder.Services
    .Configure<TelegramBotOptions>(builder.Configuration.GetSection(TelegramBotOptions.SectionName))
    .AddInfrastructure(connectionString)
    .AddSingleton<BankService>()
    .AddSingleton<TelegramCommandRouter>()
    .AddHostedService<BankBotWorker>();

var app = builder.Build();

var telegramOptions = app.Services.GetRequiredService<IOptions<TelegramBotOptions>>().Value;
var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
if (string.IsNullOrWhiteSpace(telegramOptions.Token))
{
    startupLogger.LogWarning(
        "Telegram token is not configured. Set TelegramBot:Token in appsettings.json or env TelegramBot__Token, then restart.");
}
else
{
    startupLogger.LogInformation("Telegram token is configured. Waiting for BankBotWorker to start polling...");
}

Console.WriteLine();
Console.WriteLine();
Console.WriteLine("connection string for app 1 (Bot)");
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

if (app.Environment.IsDevelopment())
{
    app.MapGet("/", () => Results.Content(TestUi.Html, MediaTypeNames.Text.Html));

    app.MapPost("/api/messages", async (
        TestChatRequest request,
        TelegramCommandRouter router,
        CancellationToken cancellationToken) =>
    {
        if (string.IsNullOrWhiteSpace(request.Command))
        {
            return Results.BadRequest(new TestChatResponse("Введите команду."));
        }

        var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
            ? $"tester-{request.UserId}"
            : request.DisplayName.Trim();
        var response = await router.HandleTextAsync(request.Command, request.UserId, displayName, cancellationToken);

        return Results.Ok(new TestChatResponse(response));
    });
}
else
{
    app.MapGet("/", () => Results.Text("My own bank bot is running."));
    app.MapGet("/health", () => Results.Text("bot"));
}

await app.RunAsync();

internal sealed record TestChatRequest(long UserId, string DisplayName, string Command);

internal sealed record TestChatResponse(string Response);

internal static class TestUi
{
    public const string Html = """
<!doctype html>
<html lang="ru">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>My own bank test chat</title>
  <style>
    :root {
      color-scheme: light;
      font-family: Inter, Segoe UI, Arial, sans-serif;
      background: #f3f4f6;
      color: #111827;
    }
    * { box-sizing: border-box; }
    body {
      margin: 0;
      min-height: 100vh;
      display: grid;
      place-items: center;
      padding: 24px;
    }
    main {
      width: min(920px, 100%);
      height: min(820px, calc(100vh - 48px));
      background: #ffffff;
      border: 1px solid #e5e7eb;
      border-radius: 24px;
      box-shadow: 0 24px 70px rgba(15, 23, 42, .12);
      display: grid;
      grid-template-rows: auto 1fr auto;
      overflow: hidden;
    }
    header {
      padding: 20px 24px;
      border-bottom: 1px solid #e5e7eb;
      display: grid;
      gap: 12px;
    }
    h1 {
      margin: 0;
      font-size: 22px;
    }
    .profile {
      display: grid;
      grid-template-columns: 140px 1fr;
      gap: 8px 12px;
      align-items: center;
    }
    label { color: #6b7280; font-size: 14px; }
    input, textarea, button {
      font: inherit;
      border-radius: 14px;
      border: 1px solid #d1d5db;
    }
    input, textarea {
      width: 100%;
      padding: 12px 14px;
      outline: none;
    }
    input:focus, textarea:focus {
      border-color: #6366f1;
      box-shadow: 0 0 0 3px rgba(99, 102, 241, .15);
    }
    #chat {
      padding: 22px;
      overflow-y: auto;
      display: flex;
      flex-direction: column;
      gap: 12px;
      background: linear-gradient(#ffffff, #f9fafb);
    }
    .message {
      max-width: 76%;
      padding: 12px 14px;
      border-radius: 18px;
      white-space: pre-wrap;
      line-height: 1.42;
    }
    .user {
      align-self: flex-end;
      background: #4f46e5;
      color: #ffffff;
      border-bottom-right-radius: 6px;
    }
    .bot {
      align-self: flex-start;
      background: #eef2ff;
      color: #1f2937;
      border-bottom-left-radius: 6px;
    }
    form {
      border-top: 1px solid #e5e7eb;
      padding: 16px;
      display: grid;
      grid-template-columns: 1fr auto;
      gap: 12px;
      align-items: end;
    }
    textarea {
      resize: none;
      min-height: 52px;
      max-height: 140px;
    }
    button {
      padding: 13px 20px;
      border: 0;
      background: #111827;
      color: #ffffff;
      cursor: pointer;
      font-weight: 700;
    }
    button:disabled {
      opacity: .6;
      cursor: wait;
    }
    .hint {
      color: #6b7280;
      font-size: 13px;
    }
    code {
      background: #f3f4f6;
      border-radius: 8px;
      padding: 2px 6px;
    }
  </style>
</head>
<body>
  <main>
    <header>
      <div>
        <h1>My own bank</h1>
        <div class="hint">Локальный интерфейс для тестирования тех же команд, что придут из Telegram.</div>
      </div>
      <div class="profile">
        <label for="userId">Telegram user id</label>
        <input id="userId" type="number" value="1001">
        <label for="displayName">Display name</label>
        <input id="displayName" value="alice">
      </div>
      <div class="hint">
        Попробуй: <code>/create Love Bank</code>, <code>/invite</code>, <code>/credit kiss 5</code>,
        <code>/openshop</code>, <code>/addproduct kiss 3 настоящий поцелуй</code>, <code>/shop</code>.
      </div>
    </header>
    <section id="chat" aria-live="polite"></section>
    <form id="form">
      <textarea id="command" placeholder="/create Love Bank" required></textarea>
      <button id="send" type="submit">Отправить</button>
    </form>
  </main>
  <script>
    const chat = document.querySelector('#chat');
    const form = document.querySelector('#form');
    const command = document.querySelector('#command');
    const send = document.querySelector('#send');

    append('bot', 'Напиши команду. Для второго человека поменяй user id/display name и используй /join <code>.');

    form.addEventListener('submit', async (event) => {
      event.preventDefault();
      const text = command.value.trim();
      if (!text) return;

      append('user', text);
      command.value = '';
      send.disabled = true;

      try {
        const response = await fetch('/api/messages', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            userId: Number(document.querySelector('#userId').value),
            displayName: document.querySelector('#displayName').value,
            command: text
          })
        });
        const data = await response.json();
        append('bot', data.response ?? 'Пустой ответ.');
      } catch (error) {
        append('bot', `Ошибка запроса: ${error}`);
      } finally {
        send.disabled = false;
        command.focus();
      }
    });

    command.addEventListener('keydown', (event) => {
      if (event.key === 'Enter' && !event.shiftKey) {
        event.preventDefault();
        form.requestSubmit();
      }
    });

    function append(kind, text) {
      const node = document.createElement('div');
      node.className = `message ${kind}`;
      node.textContent = text;
      chat.appendChild(node);
      chat.scrollTop = chat.scrollHeight;
    }
  </script>
</body>
</html>
""";
}
