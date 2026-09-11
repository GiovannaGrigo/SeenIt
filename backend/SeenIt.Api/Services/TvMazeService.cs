using System.Net;
using System.Net.Http.Json;
using SeenIt.Api.DTOs.Series;
using SeenIt.Api.External.DTOs;
using SeenIt.Api.Interfaces.Services;

namespace SeenIt.Api.Services;

public sealed class TvMazeService(HttpClient httpClient) : ITvMazeService
{
    public async Task<IReadOnlyList<PersonagemResponse>> ObterPersonagensDoEpisodioAsync(int externalSeriesId, int externalEpisodeId, CancellationToken cancellationToken)
    {
        var elencoPrincipalTask = ObterElencoPrincipalAsync(
            externalSeriesId,
            cancellationToken);

        var convidadosTask = ObterConvidadosAsync(
            externalEpisodeId,
            cancellationToken);

        await Task.WhenAll(
            elencoPrincipalTask,
            convidadosTask);

        var elencoPrincipal =
            await elencoPrincipalTask;

        var convidados =
            await convidadosTask;

        return elencoPrincipal
            .Concat(convidados)
            .Where(item => item.Character is not null)
            .DistinctBy(item => item.Character!.Id)
            .Select(item => new PersonagemResponse
            {
                ExternalCharacterId = item.Character!.Id,
                Nome = item.Character.Name,
                ImagemUrl =
                    item.Character.Image?.Medium ??
                    item.Character.Image?.Original ??
                    item.Person?.Image?.Medium ??
                    item.Person?.Image?.Original,
                AtorNome = item.Person?.Name
            })
            .OrderBy(personagem => personagem.Nome)
            .ToList();
    }

    private async Task<List<TvMazeCastItemDto>> ObterElencoPrincipalAsync(int externalSeriesId, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            $"shows/{externalSeriesId}/cast",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return [];

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<List<TvMazeCastItemDto>>(
                cancellationToken)
            ?? [];
    }

    private async Task<List<TvMazeCastItemDto>> ObterConvidadosAsync(int externalEpisodeId, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            $"episodes/{externalEpisodeId}/guestcast",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return [];

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<List<TvMazeCastItemDto>>(
                cancellationToken)
            ?? [];
    }
}