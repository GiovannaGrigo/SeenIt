namespace SeenIt.Api.DTOs.Episodios;

public sealed record EpisodioAssistidoResponse
{
    public int ExternalEpisodeId { get; init; }
    public int ExternalSeriesId { get; init; }
    public int Temporada { get; init; }
    public int NumeroEpisodio { get; init; }
    public string NomeEpisodio { get; init; } = string.Empty;
    public IReadOnlyCollection<SentimentoEpisodio> Sentimentos { get; init; } = [];
    public int ExternalCharacterId { get; init; }
    public string PersonagemFavoritoNome { get; init; } = string.Empty;
    public DateTime AssistidoEmUtc { get; init; }
}