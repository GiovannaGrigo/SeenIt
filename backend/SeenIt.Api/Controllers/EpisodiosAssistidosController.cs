using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeenIt.Api.DTOs.Episodios;
using SeenIt.Api.Interfaces.Services;

namespace SeenIt.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/episodios")]
public sealed class EpisodiosAssistidosController(IEpisodioAssistidoService episodioAssistidoService) : ControllerBase
{
    [HttpPut("{externalEpisodeId:int}/assistido")]
    [ProducesResponseType<EpisodioAssistidoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<EpisodioAssistidoResponse>> MarcarComoAssistido([FromRoute] int externalEpisodeId, [FromBody] MarcarEpisodioAssistidoRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (externalEpisodeId <= 0)
            return BadRequest("O episódio informado é inválido.");

        if (request.ExternalSeriesId <= 0)
            return BadRequest("A série informada é inválida.");

        if (request.Temporada <= 0)
            return BadRequest("A temporada informada é inválida.");

        if (request.NumeroEpisodio <= 0)
            return BadRequest("O número do episódio é inválido.");

        if (string.IsNullOrWhiteSpace(request.NomeEpisodio))
            return BadRequest("O nome do episódio é obrigatório.");

        if (request.ExternalCharacterId <= 0 || string.IsNullOrWhiteSpace(request.PersonagemFavoritoNome))
            return BadRequest("O nome do episódio é obrigatório.");


        if (request.Sentimentos is null || request.Sentimentos.Count == 0
        )
        {
                return BadRequest(
                    "Selecione pelo menos uma emoção.");
        }

        var resultado =
            await episodioAssistidoService.MarcarComoAssistidoAsync(
                userId,
                externalEpisodeId,
                request,
                cancellationToken);

        return Ok(resultado);
    }

    [HttpGet("series/{externalSeriesId:int}")]
    [ProducesResponseType<IReadOnlyList<EpisodioAssistidoResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<EpisodioAssistidoResponse>>> ObterPorSerie([FromRoute] int externalSeriesId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (externalSeriesId <= 0)
            return BadRequest("A série informada é inválida.");

        var episodios =
            await episodioAssistidoService.ObterPorSerieAsync(
                userId,
                externalSeriesId,
                cancellationToken);

        return Ok(episodios);
    }

    [HttpGet("{externalEpisodeId:int}/votacao-personagens")]
    [ProducesResponseType<IReadOnlyList<VotacaoPersonagemResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<VotacaoPersonagemResponse>>> ObterVotacaoPersonagens([FromRoute] int externalEpisodeId, CancellationToken cancellationToken)
    {
        if (externalEpisodeId <= 0)
            return BadRequest("O episódio informado é inválido.");

        var votacao =
            await episodioAssistidoService.ObterVotacaoAsync(
                externalEpisodeId,
                cancellationToken);

        return Ok(votacao);
    }

    private bool TryGetUserId(out Guid userId)
    {
        var claimValue =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return Guid.TryParse(claimValue, out userId);
    }
}