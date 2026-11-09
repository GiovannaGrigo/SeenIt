using SkiaSharp;

namespace SeenIt.Api.Services.Avatars;

public sealed class AvatarImageProcessor
{
    public const long MaxFileBytes = 5 * 1024 * 1024;
    public const long MaxRequestBytes = MaxFileBytes + 64 * 1024;
    private static readonly SemaphoreSlim Decoders = new(2);

    public async Task<ValidatedAvatar> ProcessAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0) throw new InvalidAvatarException("Selecione uma imagem.");
        if (file.Length > MaxFileBytes) throw new InvalidAvatarException("A imagem deve possuir no máximo 5 MB.");
        var claimedType = file.ContentType.ToLowerInvariant();
        if (claimedType is not ("image/jpeg" or "image/png" or "image/webp"))
            throw new InvalidAvatarException("Envie uma imagem JPG, PNG ou WEBP.");

        await using var input = file.OpenReadStream();
        using var buffer = new MemoryStream();
        var block = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(block, cancellationToken)) != 0)
        {
            if (buffer.Length + read > MaxFileBytes) throw new InvalidAvatarException("A imagem deve possuir no máximo 5 MB.");
            buffer.Write(block, 0, read);
        }
        await Decoders.WaitAsync(cancellationToken);
        try
        {
            using var data = SKData.CreateCopy(buffer.ToArray());
            using var codec = SKCodec.Create(data);
            if (codec is null) throw new InvalidAvatarException("A imagem é inválida ou está corrompida.");
            var actualType = codec.EncodedFormat switch
            {
                SKEncodedImageFormat.Jpeg => "image/jpeg",
                SKEncodedImageFormat.Png => "image/png",
                SKEncodedImageFormat.Webp => "image/webp",
                _ => null
            };
            if (actualType != claimedType) throw new InvalidAvatarException("O conteúdo da imagem não corresponde ao tipo informado.");
            var info = codec.Info;
            if (info.Width < 1 || info.Height < 1 || info.Width > 8192 || info.Height > 8192 || (long)info.Width * info.Height > 16_000_000)
                throw new InvalidAvatarException("A imagem deve possuir até 8192 pixels por lado e 16 megapixels.");
            if (codec.FrameCount > 1) throw new InvalidAvatarException("Envie uma imagem sem animação.");
            using var bitmap = new SKBitmap(new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
            if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
                throw new InvalidAvatarException("A imagem é inválida ou está corrompida.");

            var rotated = codec.EncodedOrigin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
            var width = rotated ? info.Height : info.Width;
            var height = rotated ? info.Width : info.Height;
            var scale = Math.Min(1f, 1024f / Math.Max(width, height));
            using var output = new SKBitmap(Math.Max(1, (int)(width * scale)), Math.Max(1, (int)(height * scale)), SKColorType.Rgba8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(output))
            using (var decoded = SKImage.FromBitmap(bitmap))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.Scale(scale);
                switch (codec.EncodedOrigin)
                {
                    case SKEncodedOrigin.TopRight: canvas.Translate(info.Width, 0); canvas.Scale(-1, 1); break;
                    case SKEncodedOrigin.BottomRight: canvas.Translate(info.Width, info.Height); canvas.RotateDegrees(180); break;
                    case SKEncodedOrigin.BottomLeft: canvas.Translate(0, info.Height); canvas.Scale(1, -1); break;
                    case SKEncodedOrigin.LeftTop: canvas.RotateDegrees(90); canvas.Scale(1, -1); break;
                    case SKEncodedOrigin.RightTop: canvas.Translate(info.Height, 0); canvas.RotateDegrees(90); break;
                    case SKEncodedOrigin.RightBottom: canvas.Translate(info.Height, info.Width); canvas.RotateDegrees(90); canvas.Scale(-1, 1); break;
                    case SKEncodedOrigin.LeftBottom: canvas.Translate(0, info.Width); canvas.RotateDegrees(270); break;
                }
                canvas.DrawImage(decoded, 0, 0, new SKSamplingOptions(SKFilterMode.Linear));
            }
            cancellationToken.ThrowIfCancellationRequested();
            using var image = SKImage.FromBitmap(output);
            using var encoded = image.Encode(SKEncodedImageFormat.Webp, 85);
            if (encoded is null || encoded.Size > MaxFileBytes) throw new InvalidAvatarException("Não foi possível preparar a imagem dentro do limite de 5 MB.");
            return new ValidatedAvatar(encoded.ToArray());
        }
        finally { Decoders.Release(); }
    }
}
