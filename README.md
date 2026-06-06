# My own bank

Telegram bot for private playful banks between couples.

## Architecture

- `MyOwnBank.Domain` - business model and rules: banks, members, cards, currencies, shops, products.
- `MyOwnBank.Application` - use cases and repository contracts.
- `MyOwnBank.Infrastructure` - technical implementations. It currently uses in-memory storage.
- `MyOwnBank.Bot` - Telegram entry point and command routing.
- `MyOwnBank.Tests` - domain and application tests.

Dependencies point inward:

`Bot -> Infrastructure -> Application -> Domain`

`Application -> Domain`

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
