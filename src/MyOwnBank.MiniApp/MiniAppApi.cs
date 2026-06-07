using MyOwnBank.Application.Banks;
using MyOwnBank.Domain.Banks;
using MyOwnBank.Domain.Common;
using MyOwnBank.Domain.Currencies;
using MyOwnBank.MiniApp.Storage;
using MyOwnBank.MiniApp.Telegram;

namespace MyOwnBank.MiniApp;

internal static class MiniAppApi
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/menu", GetMenuAsync);
        app.MapPost("/api/banks/join", JoinBankAsync);
        app.MapPost("/api/banks/create", CreateBankAsync);
        app.MapPost("/api/banks/{bankId:guid}", GetBankAsync);
        app.MapPost("/api/banks/{bankId:guid}/products", AddProductAsync);
        app.MapPost("/api/banks/{bankId:guid}/products/delete", RemoveProductAsync);
        app.MapPost("/api/banks/{bankId:guid}/buy", BuyProductsAsync);
        app.MapPost("/api/banks/{bankId:guid}/card-template", UploadTemplateAsync);
        app.MapPost("/api/banks/{bankId:guid}/my-card-image", UploadMyCardImageAsync);
        app.MapPost("/api/banks/{bankId:guid}/card-holder-name", UpdateCardHolderNameAsync);
        app.MapPost("/api/banks/{bankId:guid}/reset-card-image", ResetCardImageAsync);
        app.MapPost("/api/banks/{bankId:guid}/currency", UpdateCurrencyAsync);
        app.MapPost("/api/banks/{bankId:guid}/currencies/{currencyCode}/icon", UploadCurrencyIconAsync);
        app.MapPost("/api/banks/{bankId:guid}/currencies", AddCurrencyAsync);
        app.MapPost("/api/banks/{bankId:guid}/credit", CreditMemberAsync);
        app.MapPost("/api/banks/delete", DeleteMyBankAsync);
        app.MapPost("/api/banks/{bankId:guid}/delete", DeleteBankAsync);
        app.MapGet("/card-images/{bankId}/{*relativePath}", ServeImageAsync);
        app.MapGet("/currency-icons/{bankId:guid}/{fileName}", ServeCurrencyIconAsync);
    }

    private static async Task<IResult> GetMenuAsync(
        HttpContext httpContext,
        MiniAppRequest request,
        BankService bankService,
        TelegramInitDataValidator validator,
        CancellationToken cancellationToken)
    {
        if (!TryGetUser(httpContext, request.InitData, validator, out var userId, out var displayName))
        {
            return Results.Unauthorized();
        }

        var bank = await bankService.GetMyBankAsync(userId, cancellationToken);
        var banks = bank is null
            ? Array.Empty<MiniAppBankListItem>()
            : [new MiniAppBankListItem(bank.Id, bank.Name, bank.OwnerTelegramUserId == userId)];

        return Results.Ok(new MiniAppMenuResponse(userId, displayName, banks));
    }

    private static async Task<IResult> JoinBankAsync(
        HttpContext httpContext,
        MiniAppJoinBankRequest request,
        BankService bankService,
        TelegramInitDataValidator validator,
        CancellationToken cancellationToken)
    {
        if (!TryGetUser(httpContext, request.InitData, validator, out var userId, out var displayName))
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return Results.BadRequest(new MiniAppActionResponse("Введи код приглашения."));
        }

        var existing = await bankService.GetMyBankAsync(userId, cancellationToken);
        if (existing is not null)
        {
            return Results.BadRequest(new MiniAppActionResponse("Ты уже состоишь в банке."));
        }

        try
        {
            var bank = await bankService.JoinBankByCodeAsync(
                new JoinBankByCodeCommand(request.Code.Trim(), userId, displayName),
                cancellationToken);

            return Results.Ok(new MiniAppJoinBankResponse($"Ты вступил в «{bank.Name}».", bank.Id));
        }
        catch (InvalidOperationException)
        {
            return Results.BadRequest(new MiniAppActionResponse("Код недействителен, просрочен или уже использован."));
        }
    }

    private static async Task<IResult> CreateBankAsync(
        HttpContext httpContext,
        MiniAppCreateBankRequest request,
        BankService bankService,
        TelegramInitDataValidator validator,
        CancellationToken cancellationToken)
    {
        if (!TryGetUser(httpContext, request.InitData, validator, out var userId, out var displayName))
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.BadRequest(new MiniAppActionResponse("Укажи название банка."));
        }

        if (request.Currencies is null || request.Currencies.Length == 0)
        {
            return Results.BadRequest(new MiniAppActionResponse("Добавь хотя бы одну валюту."));
        }

        var existing = await bankService.GetMyBankAsync(userId, cancellationToken);
        if (existing is not null)
        {
            if (existing.OwnerTelegramUserId == userId)
            {
                return Results.Ok(new MiniAppJoinBankResponse($"Банк «{existing.Name}» уже создан.", existing.Id));
            }

            return Results.BadRequest(new MiniAppActionResponse("Ты уже состоишь в банке. Создать свой пока нельзя."));
        }

        try
        {
            var bank = await bankService.CreateBankAsync(
                new CreateBankCommand(
                    request.Name.Trim(),
                    userId,
                    displayName,
                    request.Currencies
                        .Select(item => new CreateBankCurrencyInput(item.Code, item.Name, item.Icon))
                        .ToArray()),
                cancellationToken);

            return Results.Ok(new MiniAppJoinBankResponse($"Банк «{bank.Name}» создан.", bank.Id));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new MiniAppActionResponse(ex.Message));
        }
    }

    private static async Task<IResult> GetBankAsync(
        HttpContext httpContext,
        Guid bankId,
        MiniAppRequest request,
        BankService bankService,
        CardImageStorage images,
        TelegramInitDataValidator validator,
        CancellationToken cancellationToken)
    {
        if (!TryGetUser(httpContext, request.InitData, validator, out var userId, out _))
        {
            return Results.Unauthorized();
        }

        var bank = await TryGetMemberBankAsync(bankService, userId, bankId, cancellationToken);
        if (bank is null)
        {
            return Results.NotFound();
        }

        var isOwner = bank.OwnerTelegramUserId == userId;
        var member = bank.Members.Single(item => item.TelegramUserId == userId);
        var card = bank.Cards.Single(item => item.OwnerMemberId == member.Id);
        var history = await bankService.GetHistoryAsync(userId, cancellationToken);
        var currencies = bank.Currencies
            .Take(Bank.MaxCurrencies)
            .Select(currency => new MiniAppCurrencyItem(
                currency.Code,
                currency.Name,
                string.IsNullOrWhiteSpace(currency.Icon)
                    ? Currency.ResolveDefaultIcon(currency.Code)
                    : currency.Icon))
            .ToArray();
        var members = isOwner
            ? bank.Members.Select(item =>
            {
                var memberCard = bank.Cards.SingleOrDefault(cardItem => cardItem.OwnerMemberId == item.Id);
                return new MiniAppMemberItem(
                    item.TelegramUserId,
                    item.DisplayName,
                    item.TelegramUserId == bank.OwnerTelegramUserId,
                    memberCard?.Balances ?? new Dictionary<string, decimal>());
            }).ToArray()
            : Array.Empty<MiniAppMemberItem>();

        return Results.Ok(new MiniAppBankResponse(
            bank.Id,
            bank.Name,
            isOwner,
            isOwner,
            bank.Members.Count,
            card.Id,
            card.CardNumber,
            card.HolderName,
            images.ResolveCardImageUrl(bank.Id, card.Id),
            images.CardExists(bank.Id, card.Id),
            images.TemplateExists(bank.Id),
            Bank.MaxCurrencies,
            card.Balances,
            currencies,
            members,
            bank.Products.Where(product => product.IsActive).ToArray(),
            history.ToArray()));
    }

    private static async Task<IResult> BuyProductsAsync(
        HttpContext httpContext,
        Guid bankId,
        MiniAppBuyProductsRequest request,
        BankService bankService,
        TelegramNotificationSender notifications,
        TelegramInitDataValidator validator,
        CancellationToken cancellationToken)
    {
        if (!TryGetUser(httpContext, request.InitData, validator, out var userId, out _))
        {
            return Results.Unauthorized();
        }

        var bank = await TryGetMemberBankAsync(bankService, userId, bankId, cancellationToken);
        if (bank is null)
        {
            return Results.NotFound();
        }

        if (request.ProductIds is null || request.ProductIds.Length == 0)
        {
            return Results.BadRequest(new MiniAppActionResponse("Корзина пуста."));
        }

        try
        {
            var result = await bankService.BuyProductsAsync(
                new BuyProductsCommand(userId, request.ProductIds),
                cancellationToken);

            if (result.Notification is not null)
            {
                await notifications.SendPurchaseNotificationAsync(result.Notification, cancellationToken);
            }

            return Results.Ok(new MiniAppActionResponse("Покупка оформлена."));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new MiniAppActionResponse(ex.Message));
        }
    }

    private static async Task<IResult> RemoveProductAsync(
        HttpContext httpContext,
        Guid bankId,
        MiniAppRemoveProductRequest request,
        BankService bankService,
        TelegramInitDataValidator validator,
        CancellationToken cancellationToken)
    {
        if (!TryGetUser(httpContext, request.InitData, validator, out var userId, out _))
        {
            return Results.Unauthorized();
        }

        var bank = await TryGetMemberBankAsync(bankService, userId, bankId, cancellationToken);
        if (bank is null)
        {
            return Results.NotFound();
        }

        if (bank.OwnerTelegramUserId != userId)
        {
            return Results.Forbid();
        }

        try
        {
            await bankService.RemoveProductAsync(new RemoveProductCommand(userId, request.ProductId), cancellationToken);
            return Results.Ok(new MiniAppActionResponse("Товар удалён."));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new MiniAppActionResponse(ex.Message));
        }
    }

    private static async Task<IResult> AddProductAsync(
        HttpContext httpContext,
        Guid bankId,
        MiniAppAddProductRequest request,
        BankService bankService,
        TelegramInitDataValidator validator,
        CancellationToken cancellationToken)
    {
        if (!TryGetUser(httpContext, request.InitData, validator, out var userId, out _))
        {
            return Results.Unauthorized();
        }

        var bank = await TryGetMemberBankAsync(bankService, userId, bankId, cancellationToken);
        if (bank is null)
        {
            return Results.NotFound();
        }

        if (bank.OwnerTelegramUserId != userId)
        {
            return Results.Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Name) || request.Price <= 0)
        {
            return Results.BadRequest(new MiniAppActionResponse("Укажи название и цену больше нуля."));
        }

        try
        {
            await bankService.OpenShopAsync(new OpenShopCommand(userId), cancellationToken);
            var productId = await bankService.AddProductAsync(
                new AddProductCommand(
                    userId,
                    request.Name.Trim(),
                    request.CurrencyCode,
                    request.Price,
                    string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim()),
                cancellationToken);

            return Results.Ok(new MiniAppActionResponse($"Товар добавлен.", productId));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new MiniAppActionResponse(ex.Message));
        }
    }

    private static async Task<IResult> UploadTemplateAsync(
        HttpContext httpContext,
        Guid bankId,
        HttpRequest httpRequest,
        BankService bankService,
        CardImageStorage images,
        TelegramInitDataValidator validator,
        CancellationToken cancellationToken)
    {
        if (!httpRequest.HasFormContentType)
        {
            return Results.BadRequest(new MiniAppActionResponse("Ожидается multipart/form-data."));
        }

        var form = await httpRequest.ReadFormAsync(cancellationToken);
        if (!TryGetUser(httpContext, form["initData"].ToString(), validator, out var userId, out _))
        {
            return Results.Unauthorized();
        }

        var bank = await TryGetMemberBankAsync(bankService, userId, bankId, cancellationToken);
        if (bank is null)
        {
            return Results.NotFound();
        }

        if (bank.OwnerTelegramUserId != userId)
        {
            return Results.Forbid();
        }

        var file = form.Files.GetFile("image");
        if (file is null)
        {
            return Results.BadRequest(new MiniAppActionResponse("Файл image не найден."));
        }

        try
        {
            await images.SaveTemplateAsync(bankId, file, cancellationToken);
            return Results.Ok(new MiniAppActionResponse("Шаблон карты сохранён."));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new MiniAppActionResponse(ex.Message));
        }
    }

    private static async Task<IResult> UploadMyCardImageAsync(
        HttpContext httpContext,
        Guid bankId,
        HttpRequest httpRequest,
        BankService bankService,
        CardImageStorage images,
        TelegramInitDataValidator validator,
        CancellationToken cancellationToken)
    {
        if (!httpRequest.HasFormContentType)
        {
            return Results.BadRequest(new MiniAppActionResponse("Ожидается multipart/form-data."));
        }

        var form = await httpRequest.ReadFormAsync(cancellationToken);
        if (!TryGetUser(httpContext, form["initData"].ToString(), validator, out var userId, out _))
        {
            return Results.Unauthorized();
        }

        var bank = await TryGetMemberBankAsync(bankService, userId, bankId, cancellationToken);
        if (bank is null)
        {
            return Results.NotFound();
        }

        var member = bank.Members.Single(item => item.TelegramUserId == userId);
        var card = bank.Cards.Single(item => item.OwnerMemberId == member.Id);
        var file = form.Files.GetFile("image");
        if (file is null)
        {
            return Results.BadRequest(new MiniAppActionResponse("Файл image не найден."));
        }

        try
        {
            await images.SaveCardAsync(bankId, card.Id, file, cancellationToken);
            return Results.Ok(new MiniAppActionResponse("Картинка карты сохранена."));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new MiniAppActionResponse(ex.Message));
        }
    }

    private static async Task<IResult> UpdateCardHolderNameAsync(
        HttpContext httpContext,
        Guid bankId,
        MiniAppUpdateHolderNameRequest request,
        BankService bankService,
        TelegramInitDataValidator validator,
        CancellationToken cancellationToken)
    {
        if (!TryGetUser(httpContext, request.InitData, validator, out var userId, out _))
        {
            return Results.Unauthorized();
        }

        var bank = await TryGetMemberBankAsync(bankService, userId, bankId, cancellationToken);
        if (bank is null)
        {
            return Results.NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.HolderName))
        {
            return Results.BadRequest(new MiniAppActionResponse("Укажи имя на карте."));
        }

        try
        {
            await bankService.UpdateCardHolderNameAsync(
                new UpdateCardHolderNameCommand(userId, request.HolderName),
                cancellationToken);

            return Results.Ok(new MiniAppActionResponse("Имя на карте сохранено."));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new MiniAppActionResponse(ex.Message));
        }
    }

    private static async Task<IResult> ResetCardImageAsync(
        HttpContext httpContext,
        Guid bankId,
        MiniAppRequest request,
        BankService bankService,
        CardImageStorage images,
        TelegramInitDataValidator validator,
        CancellationToken cancellationToken)
    {
        if (!TryGetUser(httpContext, request.InitData, validator, out var userId, out _))
        {
            return Results.Unauthorized();
        }

        var bank = await TryGetMemberBankAsync(bankService, userId, bankId, cancellationToken);
        if (bank is null)
        {
            return Results.NotFound();
        }

        var member = bank.Members.Single(item => item.TelegramUserId == userId);
        var card = bank.Cards.Single(item => item.OwnerMemberId == member.Id);
        var removed = images.DeleteCardImage(bankId, card.Id);

        return Results.Ok(new MiniAppActionResponse(
            removed ? "Шкурка карты сброшена." : "У карты уже стандартный дизайн."));
    }

    private static async Task<IResult> UploadCurrencyIconAsync(
        HttpContext httpContext,
        Guid bankId,
        string currencyCode,
        HttpRequest httpRequest,
        BankService bankService,
        CardImageStorage images,
        TelegramInitDataValidator validator,
        CancellationToken cancellationToken)
    {
        if (!httpRequest.HasFormContentType)
        {
            return Results.BadRequest(new MiniAppActionResponse("Ожидается multipart/form-data."));
        }

        var form = await httpRequest.ReadFormAsync(cancellationToken);
        if (!TryGetUser(httpContext, form["initData"].ToString(), validator, out var userId, out _))
        {
            return Results.Unauthorized();
        }

        var bank = await TryGetMemberBankAsync(bankService, userId, bankId, cancellationToken);
        if (bank is null)
        {
            return Results.NotFound();
        }

        if (bank.OwnerTelegramUserId != userId)
        {
            return Results.Forbid();
        }

        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            return Results.BadRequest(new MiniAppActionResponse("Укажи валюту."));
        }

        var file = form.Files.GetFile("image");
        if (file is null)
        {
            return Results.BadRequest(new MiniAppActionResponse("Файл image не найден."));
        }

        try
        {
            var iconUrl = await images.SaveCurrencyIconAsync(bankId, currencyCode, file, cancellationToken);
            await bankService.UpdateCurrencyAsync(
                new UpdateCurrencyCommand(
                    userId,
                    currencyCode.Trim(),
                    bank.Currencies.Single(item =>
                        string.Equals(item.Code, currencyCode, StringComparison.OrdinalIgnoreCase)).Name,
                    iconUrl),
                cancellationToken);

            return Results.Ok(new MiniAppCurrencyIconResponse(iconUrl));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new MiniAppActionResponse(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new MiniAppActionResponse(ex.Message));
        }
    }

    private static async Task<IResult> AddCurrencyAsync(
        HttpContext httpContext,
        Guid bankId,
        MiniAppAddCurrencyRequest request,
        BankService bankService,
        TelegramInitDataValidator validator,
        CancellationToken cancellationToken)
    {
        if (!TryGetUser(httpContext, request.InitData, validator, out var userId, out _))
        {
            return Results.Unauthorized();
        }

        var bank = await TryGetMemberBankAsync(bankService, userId, bankId, cancellationToken);
        if (bank is null)
        {
            return Results.NotFound();
        }

        if (bank.OwnerTelegramUserId != userId)
        {
            return Results.Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Code)
            || string.IsNullOrWhiteSpace(request.Name)
            || string.IsNullOrWhiteSpace(request.Icon))
        {
            return Results.BadRequest(new MiniAppActionResponse("Укажи код, название и иконку."));
        }

        try
        {
            await bankService.AddCurrencyAsync(
                new AddCurrencyCommand(userId, request.Code, request.Name, request.Icon),
                cancellationToken);

            return Results.Ok(new MiniAppActionResponse("Валюта добавлена."));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new MiniAppActionResponse(ex.Message));
        }
    }

    private static async Task<IResult> UpdateCurrencyAsync(
        HttpContext httpContext,
        Guid bankId,
        MiniAppUpdateCurrencyRequest request,
        BankService bankService,
        TelegramInitDataValidator validator,
        CancellationToken cancellationToken)
    {
        if (!TryGetUser(httpContext, request.InitData, validator, out var userId, out _))
        {
            return Results.Unauthorized();
        }

        var bank = await TryGetMemberBankAsync(bankService, userId, bankId, cancellationToken);
        if (bank is null)
        {
            return Results.NotFound();
        }

        if (bank.OwnerTelegramUserId != userId)
        {
            return Results.Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.CurrencyCode)
            || string.IsNullOrWhiteSpace(request.Name)
            || string.IsNullOrWhiteSpace(request.Icon))
        {
            return Results.BadRequest(new MiniAppActionResponse("Укажи валюту, название и иконку."));
        }

        try
        {
            await bankService.UpdateCurrencyAsync(
                new UpdateCurrencyCommand(
                    userId,
                    request.CurrencyCode.Trim(),
                    request.Name.Trim(),
                    request.Icon.Trim()),
                cancellationToken);

            return Results.Ok(new MiniAppActionResponse("Валюта сохранена."));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new MiniAppActionResponse(ex.Message));
        }
    }

    private static async Task<IResult> CreditMemberAsync(
        HttpContext httpContext,
        Guid bankId,
        MiniAppCreditRequest request,
        BankService bankService,
        TelegramInitDataValidator validator,
        CancellationToken cancellationToken)
    {
        if (!TryGetUser(httpContext, request.InitData, validator, out var userId, out _))
        {
            return Results.Unauthorized();
        }

        var bank = await TryGetMemberBankAsync(bankService, userId, bankId, cancellationToken);
        if (bank is null)
        {
            return Results.NotFound();
        }

        if (bank.OwnerTelegramUserId != userId)
        {
            return Results.Forbid();
        }

        if (request.TargetTelegramUserId <= 0 || request.Amount <= 0 || string.IsNullOrWhiteSpace(request.CurrencyCode))
        {
            return Results.BadRequest(new MiniAppActionResponse("Укажи пользователя, валюту и сумму больше нуля."));
        }

        try
        {
            await bankService.CreditMemberCardAsync(
                new CreditMemberCardCommand(
                    userId,
                    request.TargetTelegramUserId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    request.CurrencyCode.Trim(),
                    request.Amount),
                cancellationToken);

            return Results.Ok(new MiniAppActionResponse("Баланс начислен."));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new MiniAppActionResponse(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new MiniAppActionResponse(ex.Message));
        }
    }

    private static Task<IResult> DeleteMyBankAsync(
        HttpContext httpContext,
        MiniAppRequest request,
        BankService bankService,
        CardImageStorage images,
        TelegramInitDataValidator validator,
        CancellationToken cancellationToken) =>
        DeleteBankCoreAsync(httpContext, null, request, bankService, images, validator, cancellationToken);

    private static Task<IResult> DeleteBankAsync(
        HttpContext httpContext,
        Guid bankId,
        MiniAppRequest request,
        BankService bankService,
        CardImageStorage images,
        TelegramInitDataValidator validator,
        CancellationToken cancellationToken) =>
        DeleteBankCoreAsync(httpContext, bankId, request, bankService, images, validator, cancellationToken);

    private static async Task<IResult> DeleteBankCoreAsync(
        HttpContext httpContext,
        Guid? bankId,
        MiniAppRequest request,
        BankService bankService,
        CardImageStorage images,
        TelegramInitDataValidator validator,
        CancellationToken cancellationToken)
    {
        if (!TryGetUser(httpContext, request.InitData, validator, out var userId, out _))
        {
            return Results.Unauthorized();
        }

        var bank = await bankService.GetMyBankAsync(userId, cancellationToken);
        if (bank is null)
        {
            return Results.NotFound();
        }

        if (bank.OwnerTelegramUserId != userId)
        {
            return Results.Forbid();
        }

        if (bankId is Guid routeBankId && bank.Id != routeBankId)
        {
            return Results.NotFound();
        }

        try
        {
            var deletedBankId = await bankService.DeleteBankAsync(
                new DeleteBankCommand(userId, bank.Id),
                cancellationToken);
            images.DeleteBankImages(deletedBankId);

            return Results.Ok(new MiniAppActionResponse("Банк удалён."));
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new MiniAppActionResponse(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new MiniAppActionResponse(ex.Message));
        }
    }

    private static IResult ServeImageAsync(
        Guid bankId,
        string relativePath,
        CardImageStorage images)
    {
        var filePath = images.TryResolveFilePath($"card-images/{bankId:N}/{relativePath}");
        return filePath is null ? Results.NotFound() : Results.File(filePath);
    }

    private static IResult ServeCurrencyIconAsync(
        Guid bankId,
        string fileName,
        CardImageStorage images)
    {
        var filePath = images.TryResolveFilePath($"currency-icons/{bankId:N}/{fileName}");
        return filePath is null ? Results.NotFound() : Results.File(filePath);
    }

    private static async Task<BankSummary?> TryGetMemberBankAsync(
        BankService bankService,
        long userId,
        Guid bankId,
        CancellationToken cancellationToken)
    {
        var bank = await bankService.GetMyBankAsync(userId, cancellationToken);
        return bank?.Id == bankId ? bank : null;
    }

    private static bool TryGetUser(
        HttpContext httpContext,
        string initData,
        TelegramInitDataValidator validator,
        out long userId,
        out string displayName)
    {
        var environment = httpContext.RequestServices.GetRequiredService<IHostEnvironment>();
        if (environment.IsDevelopment() && initData == "local-dev")
        {
            userId = 1001;
            displayName = "alice";
            return true;
        }

        if (validator.TryValidate(initData, out userId, out displayName))
        {
            return true;
        }

        userId = 0;
        displayName = string.Empty;
        return false;
    }
}

internal sealed record MiniAppRequest(string InitData);

internal sealed record MiniAppMenuResponse(
    long UserId,
    string DisplayName,
    IReadOnlyCollection<MiniAppBankListItem> Banks);

internal sealed record MiniAppBankListItem(Guid Id, string Name, bool IsOwner);

internal sealed record MiniAppBankResponse(
    Guid Id,
    string Name,
    bool IsOwner,
    bool CanManage,
    int MemberCount,
    Guid CardId,
    string CardNumber,
    string HolderName,
    string? CardImageUrl,
    bool HasCustomSkin,
    bool HasTemplate,
    int MaxCurrencies,
    IReadOnlyDictionary<string, decimal> Balances,
    IReadOnlyCollection<MiniAppCurrencyItem> Currencies,
    IReadOnlyCollection<MiniAppMemberItem> Members,
    IReadOnlyCollection<ProductSummary> Products,
    IReadOnlyCollection<TransactionSummary> Transactions);

internal sealed record MiniAppUpdateHolderNameRequest(string InitData, string HolderName);

internal sealed record MiniAppCurrencyItem(string Code, string Name, string Icon);

internal sealed record MiniAppUpdateCurrencyRequest(string InitData, string CurrencyCode, string Name, string Icon);

internal sealed record MiniAppAddCurrencyRequest(string InitData, string Code, string Name, string Icon);

internal sealed record MiniAppMemberItem(
    long TelegramUserId,
    string DisplayName,
    bool IsOwner,
    IReadOnlyDictionary<string, decimal> Balances);

internal sealed record MiniAppCreditRequest(
    string InitData,
    long TargetTelegramUserId,
    string CurrencyCode,
    decimal Amount);

internal sealed record MiniAppAddProductRequest(string InitData, string Name, string CurrencyCode, decimal Price, string? Description = null);

internal sealed record MiniAppRemoveProductRequest(string InitData, Guid ProductId);

internal sealed record MiniAppBuyProductsRequest(string InitData, Guid[] ProductIds);

internal sealed record MiniAppActionResponse(string Message, Guid? ProductId = null);

internal sealed record MiniAppJoinBankRequest(string InitData, string Code);

internal sealed record MiniAppCreateBankCurrencyItem(string Code, string Name, string Icon);

internal sealed record MiniAppCurrencyIconResponse(string Icon);

internal sealed record MiniAppCreateBankRequest(
    string InitData,
    string Name,
    MiniAppCreateBankCurrencyItem[] Currencies);

internal sealed record MiniAppJoinBankResponse(string Message, Guid BankId);
