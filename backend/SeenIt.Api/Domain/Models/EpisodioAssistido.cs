using SeenIt.Api.Domain;

public sealed class EpisodioAssistido
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public int ExternalSeriesId { get; set; }
    public int ExternalEpisodeId { get; set; }
    public int Temporada { get; set; }
    public int NumeroEpisodio { get; set; }
    public string NomeEpisodio { get; set; } = string.Empty;
    public ICollection<EpisodioAssistidoSentimento> Sentimentos { get; set; } = new List<EpisodioAssistidoSentimento>();
    public int ExternalCharacterId { get; set; }
    public string PersonagemFavoritoNome { get; set; } = string.Empty;
    public DateTime AssistidoEmUtc { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
}
