using Microsoft.Extensions.Options;

namespace SeenIt.Api.Services.Avatars;

public sealed class AvatarStorageService(
    SupabaseAvatarStorageService supabase,
    IOptions<AvatarStorageOptions> options,
    IWebHostEnvironment environment,
    ILogger<AvatarStorageService> logger) : IAvatarStorageService
{
    public async Task<string> UploadAsync(Guid userId, ValidatedAvatar image, CancellationToken cancellationToken)
    {
        if (options.Value.Enabled) return await supabase.UploadAsync(userId, image, cancellationToken);
        if (environment.IsProduction()) throw new AvatarStorageException();
        if (userId == Guid.Empty || image.Bytes.Length is 0 or > (int)AvatarImageProcessor.MaxFileBytes) throw new InvalidAvatarException("Imagem ou usuário inválido.");
        var path = AvatarPaths.New(userId, local: true);
        var filePath = LocalFile(path);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var created = false;
        try
        {
            await using var file = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            created = true;
            await file.WriteAsync(image.Bytes, cancellationToken);
        }
        catch
        {
            // Only this newly generated path is eligible for compensation.
            if (created && File.Exists(filePath)) File.Delete(filePath);
            throw;
        }
        return path;
    }

    public Task DeleteAsync(Guid userId, string path, CancellationToken cancellationToken)
    {
        if (AvatarPaths.IsLegacy(path))
        {
            logger.LogWarning("AvatarLegacyRetained UserId={UserId} ObjectPath={ObjectPath}; preserve until manually migrated and audited", userId, path);
            return Task.CompletedTask;
        }
        if (!AvatarPaths.IsOwned(userId, path)) throw new AvatarStorageException();
        if (!AvatarPaths.IsLocal(path))
        {
            if (options.Value.Enabled) return supabase.DeleteAsync(userId, path, cancellationToken);
            logger.LogWarning("AvatarCleanupRequired UserId={UserId} ObjectPath={ObjectPath} Reason=SupabaseDisabled; verify reference before deleting", userId, path);
            return Task.CompletedTask;
        }
        cancellationToken.ThrowIfCancellationRequested();
        File.Delete(LocalFile(path));
        return Task.CompletedTask;
    }

    public string? GetPublicUrl(Guid userId, string? path, HttpRequest request)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (AvatarPaths.IsLegacy(path) || AvatarPaths.IsLocal(path) && AvatarPaths.IsOwned(userId, path))
            return $"{request.Scheme}://{request.Host}{request.PathBase}/uploads/profiles/{path}";
        return supabase.GetPublicUrl(userId, path);
    }

    private string LocalFile(string path) => Path.Combine(
        environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"),
        "uploads", "profiles", path.Replace('/', Path.DirectorySeparatorChar));
}
