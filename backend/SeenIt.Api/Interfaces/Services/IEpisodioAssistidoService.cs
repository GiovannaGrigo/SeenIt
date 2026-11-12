using SeenIt.Api.DTOs.Episodios;

namespace SeenIt.Api.Interfaces.Services;

public interface IEpisodioAssistidoService
{
    Task<IReadOnlyList<EpisodioAssistidoResponse>> DesmarcarTemporadaAsync(
        Guid userId,
        int externalSeriesId,
        int temporada,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<EpisodioAssistidoResponse>> MarcarTemporadaComoAssistidaAsync(
        Guid userId,
        int externalSeriesId,
        int temporada,
        CancellationToken cancellationToken);

    Task PrepararTodosComoAssistidosAsync(
        Guid userId,
        int externalSeriesId,
        IReadOnlyCollection<SeenIt.Api.DTOs.EpisodeResponse> episodios,
        CancellationToken cancellationToken);

    Task<EpisodioAssistidoResponse> MarcarComoAssistidoAsync(
        Guid userId,
        int externalEpisodeId,
        MarcarEpisodioAssistidoRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<EpisodioAssistidoResponse>> ObterPorSerieAsync(
        Guid userId,
        int externalSeriesId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<VotacaoPersonagemResponse>> ObterVotacaoAsync(
        int externalEpisodeId,
        CancellationToken cancellationToken);
}
