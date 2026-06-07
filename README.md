# My own bank

Telegram bot for private playful banks between couples.

## Architecture

Подробная схема (слои, домен, Mini App, API, магазин/корзина): **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)**.

Кратко:

- `MyOwnBank.Domain` — бизнес-модель: банки, карты, валюты, магазин, товары.
- `MyOwnBank.Application` — сценарии (`BankService`), контракты репозиториев.
- `MyOwnBank.Infrastructure` — SQLite, EF Core, маппинг.
- `MyOwnBank.Bot` — Telegram-бот и команды.
- `MyOwnBank.MiniApp` — Mini App (SPA + REST API).
- `MyOwnBank.Tests` — тесты домена.

Зависимости: `Bot / MiniApp → Infrastructure → Application → Domain`.

## Current commands

- `/newbank <name>` creates a bank and owner card.
- `/balance` shows card balances.

## Local run

Set the bot token with an environment variable:

```powershell
$env:TelegramBot__Token = "YOUR_TOKEN"
dotnet run --project .\src\MyOwnBank.Bot\MyOwnBank.Bot.csproj
```

## Notes

The first three currencies are seeded by the domain:

- `hug` - обнимашки
- `kiss` - поцелуйчики
- `spank` - порка
