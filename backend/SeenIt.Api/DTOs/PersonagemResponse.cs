namespace SeenIt.Api.DTOs.Series;

public sealed record PersonagemResponse
{
    public int ExternalCharacterId { get; init; }
    public string Nome { get; init; } = string.Empty;
    public string? ImagemUrl { get; init; }
    public string? AtorNome { get; init; }
}