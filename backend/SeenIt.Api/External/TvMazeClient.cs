using System.Net;
using System.Text.RegularExpressions;
using SeenIt.Api.DTOs;

namespace SeenIt.Api.External;

public sealed partial class TvMazeClient(HttpClient httpClient)
{
    public async Task<IReadOnlyCollection<SeriesSummaryResponse>> SearchAsync(
        string query,
        CancellationToken cancellationToken)
    {
        var results = await httpClient.GetFromJsonAsync<List<TvMazeSearchResult>>(
            $"search/shows?q={Uri.EscapeDataString(query)}", cancellationToken) ?? [];

        return results.Select(x => new SeriesSummaryResponse(
            x.Show.Id,
            x.Show.Name,
            x.Show.Image?.Medium,
            CleanHtml(x.Show.Summary),
            x.Show.Premiered,
            x.Show.Ended,
            x.Show.Rating?.Average,
            x.Show.Genres ?? [])).ToArray();
    }

    public async Task<SeriesDetailsResponse?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"shows/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();

        var show = await response.Content.ReadFromJsonAsync<TvMazeShow>(cancellationToken);
        if (show is null) return null;

        return new SeriesDetailsResponse(
            show.Id,
            show.Name,
            show.Image?.Medium,
            show.Image?.Original,
            CleanHtml(show.Summary),
            show.Premiered,
            show.Ended,
            show.Network?.Name ?? show.WebChannel?.Name,
            show.Status,
            show.Rating?.Average,
            show.Genres ?? []);
    }

    public async Task<IReadOnlyCollection<EpisodeResponse>> GetEpisodesAsync(
        int seriesId,
        CancellationToken cancellationToken)
    {
        var episodes = await httpClient.GetFromJsonAsync<List<TvMazeEpisode>>(
            $"shows/{seriesId}/episodes", cancellationToken) ?? [];

        return episodes.Select(x => new EpisodeResponse(
            x.Id,
            x.Season,
            x.Number,
            x.Name,
            CleanHtml(x.Summary),
            x.Airdate,
            x.Runtime,
            x.Image?.Medium)).ToArray();
    }

    private static string? CleanHtml(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return WebUtility.HtmlDecode(HtmlTagRegex().Replace(value, string.Empty)).Trim();
    }

    [GeneratedRegex("<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();
}
