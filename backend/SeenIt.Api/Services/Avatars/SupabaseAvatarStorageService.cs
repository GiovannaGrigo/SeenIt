using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace SeenIt.Api.Services.Avatars;

public sealed class SupabaseAvatarStorageService(
    IHttpClientFactory clients,
    IOptions<AvatarStorageOptions> options,
    ILogger<SupabaseAvatarStorageService> logger)
{
    public const string ClientName = "SupabaseAvatars";
    private AvatarStorageOptions Settings => options.Value;

    public string? GetPublicUrl(Guid userId, string path)
    {
        if (!AvatarPaths.IsOwned(userId, path) || AvatarPaths.IsLocal(path) || !AvatarStorageOptions.IsValidUrl(Settings.Url)) return null;
        return $"{Settings.Url.TrimEnd('/')}/storage/v1/object/public/{Settings.Bucket}/{path}";
    }

    public async Task<string> UploadAsync(Guid userId, ValidatedAvatar image, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || image.Bytes.Length is 0 or > (int)AvatarImageProcessor.MaxFileBytes)
            throw new InvalidAvatarException("Imagem ou usuário inválido.");
        var path = AvatarPaths.New(userId);
        using var request = CreateRequest(HttpMethod.Post, $"object/{Settings.Bucket}/{path}");
        request.Headers.Add("x-upsert", "false");
        request.Content = new ByteArrayContent(image.Bytes);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(ValidatedAvatar.ContentType);
        try
        {
            using var response = await clients.CreateClient(ClientName).SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode) return path;
            // A conflict may concern an already-existing object; never delete it.
            if ((int)response.StatusCode >= 500) await CleanupFailedUpload(userId, path);
            throw new AvatarStorageException();
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException)
        {
            await CleanupFailedUpload(userId, path);
            if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);
            throw new AvatarStorageException();
        }
    }

    public async Task DeleteAsync(Guid userId, string path, CancellationToken cancellationToken)
    {
        if (!AvatarPaths.IsOwned(userId, path) || AvatarPaths.IsLocal(path)) throw new AvatarStorageException();
        using var request = CreateRequest(HttpMethod.Delete, $"object/{Settings.Bucket}");
        request.Content = JsonContent.Create(new { prefixes = new[] { path } });
        try
        {
            using var response = await clients.CreateClient(ClientName).SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound) throw new AvatarStorageException();
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);
            throw new AvatarStorageException();
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string resource)
    {
        if (!Settings.IsValid() || string.IsNullOrEmpty(Settings.SecretKey) || string.IsNullOrEmpty(Settings.Url)) throw new AvatarStorageException();
        var request = new HttpRequestMessage(method, new Uri($"{Settings.Url.TrimEnd('/')}/storage/v1/{resource}"));
        request.Headers.Add("apikey", Settings.SecretKey);
        // sb_secret_ is an API key, not a JWT. Never add an Authorization bearer header.
        return request;
    }

    private async Task CleanupFailedUpload(Guid userId, string path)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try { await DeleteAsync(userId, path, timeout.Token); }
        catch (Exception error) when (error is AvatarStorageException or OperationCanceledException)
        {
            logger.LogWarning("AvatarCleanupRequired UserId={UserId} ObjectPath={ObjectPath} Reason=UploadOutcomeUnknown", userId, path);
        }
    }
}
