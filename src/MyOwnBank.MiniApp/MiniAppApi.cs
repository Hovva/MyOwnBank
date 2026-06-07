using MyOwnBank.Application.Banks;
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
        app.MapPost("/api/banks/{bankId:guid}", GetBankAsync);
        app.MapPost("/api/banks/{bankId:guid}/products", AddProductAsync);
        app.MapPost("/api/banks/{bankId:guid}/card-template", UploadTemplateAsync);
        app.MapPost("/api/banks/{bankId:guid}/my-card-image", UploadMyCardImageAsync);
        app.MapGet("/card-images/{bankId}/{*relativePath}", ServeImageAsync);
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

        return Results.Ok(new MiniAppBankResponse(
            bank.Id,
            bank.Name,
            isOwner,
            isOwner,
            bank.Members.Count,
            card.Id,
            images.ResolveCardImageUrl(bank.Id, card.Id),
            images.TemplateExists(bank.Id),
            card.Balances,
            Currency.DefaultCurrencies
                .Select(item => new MiniAppCurrencyItem(item.Code, item.Name))
                .ToArray(),
            bank.Products.Where(product => product.IsActive).ToArray(),
            history.ToArray()));
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
                new AddProductCommand(userId, request.Name.Trim(), request.CurrencyCode, request.Price),
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

    private static IResult ServeImageAsync(
        Guid bankId,
        string relativePath,
        CardImageStorage images)
    {
        var filePath = images.TryResolveFilePath($"card-images/{bankId:N}/{relativePath}");
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
    string? CardImageUrl,
    bool HasTemplate,
    IReadOnlyDictionary<string, decimal> Balances,
    IReadOnlyCollection<MiniAppCurrencyItem> Currencies,
    IReadOnlyCollection<ProductSummary> Products,
    IReadOnlyCollection<TransactionSummary> Transactions);

internal sealed record MiniAppCurrencyItem(string Code, string Name);

internal sealed record MiniAppAddProductRequest(string InitData, string Name, string CurrencyCode, decimal Price);

internal sealed record MiniAppActionResponse(string Message, Guid? ProductId = null);
