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

    public string? TryResolveFilePath(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/').Trim('/');
        if (!normalized.StartsWith("card-images/", StringComparison.Ordinal))
        {
            return null;
        }

        var candidate = Path.Combine(_root, normalized["card-images/".Length..].Replace('/', Path.DirectorySeparatorChar));
        var fullRoot = Path.GetFullPath(_root);
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
}
