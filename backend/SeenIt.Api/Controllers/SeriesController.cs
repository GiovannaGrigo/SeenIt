using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeenIt.Api.DTOs.Series;
using SeenIt.Api.Interfaces.Services;

namespace SeenIt.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/series")]
public sealed class SeriesController(
    ITvMazeService tvMazeService)
    : ControllerBase
{
    [HttpGet("{seriesId:int}/episodios/{episodeId:int}/personagens")]
    [ProducesResponseType<IReadOnlyList<PersonagemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<PersonagemResponse>>> ObterPersonagensDoEpisodio([FromRoute] int seriesId, [FromRoute] int episodeId, CancellationToken cancellationToken)
    {
        if (seriesId <= 0)
            return BadRequest("O identificador da série é inválido.");

        if (episodeId <= 0)
            return BadRequest("O identificador do episódio é inválido.");

        var personagens =
            await tvMazeService
                .ObterPersonagensDoEpisodioAsync(
                    seriesId,
                    episodeId,
                    cancellationToken);

        return Ok(personagens);
    }
}