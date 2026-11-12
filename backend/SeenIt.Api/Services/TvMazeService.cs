using System.Net;
using SeenIt.Api.External;
using System.Net.Http.Json;
using SeenIt.Api.DTOs.Series;
using SeenIt.Api.External.DTOs;
using SeenIt.Api.Interfaces.Services;

namespace SeenIt.Api.Services;

public sealed class TvMazeService(HttpClient httpClient, TvMazeClient catalog, CatalogCache cache) : ITvMazeService
{
    public async Task<IReadOnlyList<PersonagemResponse>> ObterPersonagensDoEpisodioAsync(int externalSeriesId, int externalEpisodeId, CancellationToken cancellationToken)
    {
        DataIntegrity.Require(externalSeriesId > 0 && externalEpisodeId > 0);
        var episodes = await catalog.GetEpisodesAsync(externalSeriesId, cancellationToken);
        DataIntegrity.Require(episodes.Any(x => x.Id == externalEpisodeId), "Episódio não pertence à série.");
        var elencoPrincipalTask = ObterElencoPrincipalAsync(
            externalSeriesId,
            cancellationToken);

        var convidadosTask = ObterConvidadosAsync(
            externalEpisodeId,
            cancellationToken);

        try { await Task.WhenAll(elencoPrincipalTask, convidadosTask); }
        catch (CatalogNotFoundException)
        {
            cache.Invalidate($"episodes:{externalSeriesId}");
            cache.Invalidate($"episode-show:{externalEpisodeId}");
            throw;
        }

        var elencoPrincipal =
            await elencoPrincipalTask;

        var convidados =
            await convidadosTask;

        if (elencoPrincipal.Concat(convidados).Any(x => x is null || x.Character is null ||
            x.Character.Id <= 0 || !DataIntegrity.Text(x.Character.Name, 200, true)))
            throw new CatalogException("Invalid character response.");
        var result = elencoPrincipal
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
        return result;
    }

    private Task<List<TvMazeCastItemDto>> ObterElencoPrincipalAsync(int externalSeriesId, CancellationToken cancellationToken) =>
        cache.GetAsync($"show-cast:{externalSeriesId}", ct => LoadCastAsync($"shows/{externalSeriesId}/cast", externalSeriesId, null, ct), cancellationToken);

    private Task<List<TvMazeCastItemDto>> ObterConvidadosAsync(int externalEpisodeId, CancellationToken cancellationToken) =>
        cache.GetAsync($"guest-cast:{externalEpisodeId}", ct => LoadCastAsync($"episodes/{externalEpisodeId}/guestcast", null, externalEpisodeId, ct), cancellationToken);

    private async Task<List<TvMazeCastItemDto>> LoadCastAsync(string path, int? seriesId, int? episodeId, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(path, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            if (seriesId.HasValue) { cache.Invalidate($"episodes:{seriesId}"); cache.Invalidate($"show:{seriesId}"); }
            if (episodeId.HasValue) cache.Invalidate($"episode-show:{episodeId}");
            throw new CatalogNotFoundException();
        }
        response.EnsureSuccessStatusCode();
        var items = await response.Content.ReadFromJsonAsync<List<TvMazeCastItemDto>>(cancellationToken)
            ?? throw new CatalogException("Null cast response.");
        if (items.Any(x => x is null || x.Character is null || x.Character.Id <= 0 || !DataIntegrity.Text(x.Character.Name, 200, true)))
            throw new CatalogException("Invalid character response.");
        return items;
    }
}
