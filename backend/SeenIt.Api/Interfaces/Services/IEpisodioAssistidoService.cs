using SeenIt.Api.DTOs.Episodios;

namespace SeenIt.Api.Interfaces.Services;

public interface IEpisodioAssistidoService
{
    Task MarcarTodosComoAssistidosAsync(
        Guid userId,
        int externalSeriesId,
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
