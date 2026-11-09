namespace SeenIt.Api.Services.Avatars;

public sealed class AvatarProfileService(
    AvatarImageProcessor images,
    IAvatarStorageService storage,
    IAvatarReferenceStore references,
    ILogger<AvatarProfileService> logger)
{
    public async Task<string> ReplaceAsync(Guid userId, string? previous, IFormFile image, CancellationToken cancellationToken)
    {
        var validated = await images.ProcessAsync(image, cancellationToken);
        var uploaded = await storage.UploadAsync(userId, validated, cancellationToken);
        bool changed;
        try { changed = await references.CompareExchangeAsync(userId, previous, uploaded, cancellationToken); }
        catch
        {
            // A database timeout may occur AFTER commit. Never delete an object still referenced.
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                if (!await references.IsReferencedAsync(userId, uploaded, timeout.Token)) await CleanupAsync(userId, uploaded);
                else if (!string.IsNullOrEmpty(previous)) LogUnknownOutcome(userId, previous);
            }
            catch
            {
                logger.LogWarning("AvatarCleanupRequired UserId={UserId} ObjectPath={ObjectPath} Reason=DatabaseOutcomeUnknown; verify reference before deleting", userId, uploaded);
            }
            throw;
        }
        if (!changed)
        {
            await CleanupAsync(userId, uploaded);
            throw new AvatarConflictException();
        }
        if (!string.IsNullOrEmpty(previous)) await CleanupAsync(userId, previous);
        return uploaded;
    }

    public async Task RemoveAsync(Guid userId, string? previous, CancellationToken cancellationToken)
    {
        bool changed;
        try { changed = await references.CompareExchangeAsync(userId, previous, null, cancellationToken); }
        catch
        {
            if (!string.IsNullOrEmpty(previous)) LogUnknownOutcome(userId, previous);
            throw;
        }
        if (!changed) throw new AvatarConflictException();
        if (!string.IsNullOrEmpty(previous)) await CleanupAsync(userId, previous);
    }

    public async Task DeleteAccountAsync(Guid userId, string? previous, string passwordHash, CancellationToken cancellationToken)
    {
        bool deleted;
        try { deleted = await references.DeleteAccountAsync(userId, previous, passwordHash, cancellationToken); }
        catch
        {
            if (!string.IsNullOrEmpty(previous)) LogUnknownOutcome(userId, previous);
            throw;
        }
        if (!deleted) throw new AvatarConflictException();
        if (!string.IsNullOrEmpty(previous)) await CleanupAsync(userId, previous);
    }

    private async Task CleanupAsync(Guid userId, string path)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try { await storage.DeleteAsync(userId, path, timeout.Token); }
        catch
        {
            // No raw exceptions, response bodies, headers, key values or connection strings in logs.
            logger.LogWarning("AvatarCleanupRequired UserId={UserId} ObjectPath={ObjectPath}; verify reference before deleting", userId, path);
        }
    }

    private void LogUnknownOutcome(Guid userId, string path) =>
        logger.LogWarning("AvatarCleanupRequired UserId={UserId} ObjectPath={ObjectPath} Reason=DatabaseOutcomeUnknown; verify reference before deleting", userId, path);
}
