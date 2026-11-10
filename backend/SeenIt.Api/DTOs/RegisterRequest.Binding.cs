using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace SeenIt.Api.DTOs;

public sealed partial record RegisterRequest
{
    // Accommodates all fields even when each character uses a JSON Unicode escape.
    public const int MaxBodyBytes = 16 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Registration only. Bound the read even for chunked requests without Content-Length.
    public static async ValueTask<RegisterRequest?> BindAsync(HttpContext context)
    {
        if (context.Request.ContentLength > MaxBodyBytes)
            throw new BadHttpRequestException("Corpo do cadastro muito grande.", StatusCodes.Status413PayloadTooLarge);
        if (!context.Request.HasJsonContentType())
            throw new BadHttpRequestException("Envie o cadastro como JSON.", StatusCodes.Status415UnsupportedMediaType);
        var bytes = new byte[MaxBodyBytes + 1];
        var length = 0;
        while (length < bytes.Length)
        {
            var read = await context.Request.Body.ReadAsync(bytes.AsMemory(length), context.RequestAborted);
            if (read == 0) break;
            length += read;
        }
        if (length > MaxBodyBytes)
            throw new BadHttpRequestException("Corpo do cadastro muito grande.", StatusCodes.Status413PayloadTooLarge);
        try
        {
            return JsonSerializer.Deserialize<RegisterRequest>(bytes.AsSpan(0, length), JsonOptions)
                ?? throw new BadHttpRequestException("Informe um objeto JSON de cadastro.");
        }
        catch (JsonException)
        {
            throw new BadHttpRequestException("JSON de cadastro inválido.");
        }
    }
}
