using MyOwnBank.Domain.Currencies;
using MyOwnBank.MiniApp.Persistence;

namespace MyOwnBank.MiniApp.Storage;

public sealed class CardImageStorage(IConfiguration configuration)
{
    private const long MaxFileSizeBytes = 2 * 1024 * 1024;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private readonly string _root = ResolveImagesRoot(configuration);
    private readonly string _currencyRoot = ResolveCurrencyIconsRoot(configuration);

    public string TemplatePath(Guid bankId) => Path.Combine(_root, bankId.ToString("N"), "template.jpg");

    public string CardPath(Guid bankId, Guid cardId) => Path.Combine(_root, bankId.ToString("N"), "cards", $"{cardId:N}.jpg");

    public bool TemplateExists(Guid bankId) => File.Exists(TemplatePath(bankId));

    public bool CardExists(Guid bankId, Guid cardId) => File.Exists(CardPath(bankId, cardId));

    public string? ResolveCardImageUrl(Guid bankId, Guid cardId)
    {
        if (CardExists(bankId, cardId))
        {
            return $"/card-images/{bankId:N}/cards/{cardId:N}.jpg";
        }

        if (TemplateExists(bankId))
        {
            return $"/card-images/{bankId:N}/template.jpg";
        }

        return null;
    }

    public async Task SaveTemplateAsync(Guid bankId, IFormFile file, CancellationToken cancellationToken)
    {
        var destination = TemplatePath(bankId);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await SaveValidatedAsync(file, destination, cancellationToken);
    }

    public async Task SaveCardAsync(Guid bankId, Guid cardId, IFormFile file, CancellationToken cancellationToken)
    {
        var destination = CardPath(bankId, cardId);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await SaveValidatedAsync(file, destination, cancellationToken);
    }

    public string BuildCurrencyIconUrl(Guid bankId, string currencyCode, string extension) =>
        $"{CurrencyIcon.ImagePrefix}{bankId:N}/{NormalizeCurrencyCode(currencyCode)}{extension}";

    public async Task<string> SaveCurrencyIconAsync(
        Guid bankId,
        string currencyCode,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCurrencyCode(currencyCode);
        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Допустимы только JPG, PNG и WEBP.");
        }

        var bankFolder = Path.Combine(_currencyRoot, bankId.ToString("N"));
        Directory.CreateDirectory(bankFolder);

        foreach (var existingExtension in AllowedExtensions)
        {
            var existingPath = Path.Combine(bankFolder, $"{normalizedCode}{existingExtension}");
            if (File.Exists(existingPath))
            {
                File.Delete(existingPath);
            }
        }

        var destination = Path.Combine(bankFolder, $"{normalizedCode}{extension}");
        await SaveValidatedAsync(file, destination, cancellationToken);

        return BuildCurrencyIconUrl(bankId, normalizedCode, extension);
    }

    public void DeleteBankImages(Guid bankId)
    {
        var bankFolder = Path.Combine(_root, bankId.ToString("N"));
        if (Directory.Exists(bankFolder))
        {
            Directory.Delete(bankFolder, recursive: true);
        }

        var currencyFolder = Path.Combine(_currencyRoot, bankId.ToString("N"));
        if (Directory.Exists(currencyFolder))
        {
            Directory.Delete(currencyFolder, recursive: true);
        }
    }

    public bool DeleteCardImage(Guid bankId, Guid cardId)
    {
        var destination = CardPath(bankId, cardId);
        if (!File.Exists(destination))
        {
            return false;
        }

        File.Delete(destination);
        return true;
    }

    public string? TryResolveFilePath(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/').Trim('/');

        if (normalized.StartsWith("card-images/", StringComparison.Ordinal))
        {
            return TryResolveUnderRoot(_root, normalized["card-images/".Length..]);
        }

        if (normalized.StartsWith("currency-icons/", StringComparison.Ordinal))
        {
            return TryResolveUnderRoot(_currencyRoot, normalized["currency-icons/".Length..]);
        }

        return null;
    }

    private static string? TryResolveUnderRoot(string root, string relativePath)
    {
        var candidate = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var fullRoot = Path.GetFullPath(root);
        var fullCandidate = Path.GetFullPath(candidate);

        if (!fullCandidate.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullCandidate))
        {
            return null;
        }

        return fullCandidate;
    }

    private static async Task SaveValidatedAsync(IFormFile file, string destination, CancellationToken cancellationToken)
    {
        if (file.Length is <= 0 or > MaxFileSizeBytes)
        {
            throw new InvalidOperationException("Изображение должно быть от 1 байта до 2 МБ.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Допустимы только JPG, PNG и WEBP.");
        }

        await using var input = file.OpenReadStream();
        await using var output = File.Create(destination);
        await input.CopyToAsync(output, cancellationToken);
    }

    private static string ResolveImagesRoot(IConfiguration configuration)
    {
        var mountPath = configuration["Persistence:MountPath"] ?? PersistencePaths.DefaultMountPath;
        var root = Path.Combine(mountPath, "card-images");
        Directory.CreateDirectory(root);
        return root;
    }

    private static string ResolveCurrencyIconsRoot(IConfiguration configuration)
    {
        var mountPath = configuration["Persistence:MountPath"] ?? PersistencePaths.DefaultMountPath;
        var root = Path.Combine(mountPath, "currency-icons");
        Directory.CreateDirectory(root);
        return root;
    }

    private static string NormalizeCurrencyCode(string currencyCode) =>
        currencyCode.Trim().ToLowerInvariant();
}
