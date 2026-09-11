using SeenIt.Api.DTOs.Series;

namespace SeenIt.Api.Interfaces.Services;

public interface ITvMazeService
{
    Task<IReadOnlyList<PersonagemResponse>> ObterPersonagensDoEpisodioAsync(int externalSeriesId, int externalEpisodeId, CancellationToken cancellationToken);
}