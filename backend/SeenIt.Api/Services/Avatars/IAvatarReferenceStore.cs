using Microsoft.EntityFrameworkCore;
using SeenIt.Api.Data;

namespace SeenIt.Api.Services.Avatars;

public interface IAvatarReferenceStore
{
    Task<bool> CompareExchangeAsync(Guid userId, string? expected, string? replacement, CancellationToken cancellationToken);
    Task<bool> IsReferencedAsync(Guid userId, string path, CancellationToken cancellationToken);
    Task<bool> DeleteAccountAsync(Guid userId, string? expectedAvatar, string expectedPasswordHash, CancellationToken cancellationToken);
}

public sealed class AvatarReferenceStore(AppDbContext database) : IAvatarReferenceStore
{
    public async Task<bool> CompareExchangeAsync(Guid userId, string? expected, string? replacement, CancellationToken cancellationToken) =>
        await database.Users.Where(u => u.Id == userId && u.AvatarFileName == expected)
            .ExecuteUpdateAsync(update => update.SetProperty(u => u.AvatarFileName, replacement), cancellationToken) == 1;

    public Task<bool> IsReferencedAsync(Guid userId, string path, CancellationToken cancellationToken) =>
        database.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.AvatarFileName == path, cancellationToken);

    public async Task<bool> DeleteAccountAsync(Guid userId, string? expectedAvatar, string expectedPasswordHash, CancellationToken cancellationToken) =>
        await database.Users.Where(u => u.Id == userId && u.AvatarFileName == expectedAvatar && u.PasswordHash == expectedPasswordHash)
            .ExecuteDeleteAsync(cancellationToken) == 1;
}
