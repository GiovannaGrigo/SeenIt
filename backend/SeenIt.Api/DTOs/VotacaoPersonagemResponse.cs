namespace SeenIt.Api.DTOs.Episodios;

public sealed record VotacaoPersonagemResponse
{
    public int ExternalCharacterId { get; init; }
    public string PersonagemNome { get; init; } = string.Empty;
    public int TotalVotos { get; init; }
    public decimal Porcentagem { get; init; }
}