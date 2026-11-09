namespace SeenIt.Api.Services.Avatars;

public interface IAvatarStorageService
{
    Task<string> UploadAsync(Guid userId, ValidatedAvatar image, CancellationToken cancellationToken);
    Task DeleteAsync(Guid userId, string path, CancellationToken cancellationToken);
    string? GetPublicUrl(Guid userId, string? path, HttpRequest request);
}

public sealed record ValidatedAvatar(byte[] Bytes)
{
    public const string ContentType = "image/webp";
}

public sealed class InvalidAvatarException(string message) : Exception(message);
public sealed class AvatarConflictException() : Exception("O perfil foi alterado. Atualize a página e tente novamente.");
public sealed class AvatarStorageException() : Exception("Não foi possível acessar o armazenamento de avatares.");
