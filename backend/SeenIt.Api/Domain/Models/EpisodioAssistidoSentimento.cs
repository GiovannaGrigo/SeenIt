namespace SeenIt.Api.Domain;

public sealed class EpisodioAssistidoSentimento
{
    public Guid EpisodioAssistidoId { get; set; }
    public SentimentoEpisodio Sentimento { get; set; }
    public EpisodioAssistido EpisodioAssistido { get; set; } = null!;
}