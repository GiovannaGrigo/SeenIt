using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using SeenIt.Api.DTOs;
using SeenIt.Api.Services;

namespace SeenIt.Api.External;

public sealed partial class TvMazeClient(HttpClient httpClient, IMemoryCache cache, ILogger<TvMazeClient> logger, CatalogCache shared)
{
    public async Task<bool?> IsAnimeAsync(int id, CancellationToken cancellationToken)
    {
        var key = $"tvmaze:anime:{id}";
        if (cache.TryGetValue<bool>(key, out var cached)) return cached;

        try
        {
            var show = await GetByIdAsync(id, cancellationToken);
            if (show is null) return null;
            var isAnime = show.Genres.Contains("Anime", StringComparer.OrdinalIgnoreCase);
            cache.Set(key, isAnime, TimeSpan.FromHours(24));
            return isAnime;
        }
        catch (Exception error) when (error is HttpRequestException or CatalogException or System.Text.Json.JsonException ||
            (error is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // Keep unknown titles eligible for classification on the next load.
            logger.LogWarning(error, "Could not classify TVmaze show {SeriesId}", id);
            return null;
        }
    }

    public async Task<IReadOnlyCollection<SeriesSummaryResponse>> SearchAsync(
        string query,
        CancellationToken cancellationToken)
    {
        var results = await httpClient.GetFromJsonAsync<List<TvMazeSearchResult>>(
            $"search/shows?q={Uri.EscapeDataString(query)}", cancellationToken) ?? throw new CatalogException("Null search response.");
        if (results.Any(x => x is null || x.Show is null || x.Show.Id <= 0 || !DataIntegrity.Text(x.Show.Name, 240, true)))
            throw new CatalogException("Invalid search response.");

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

    public Task<SeriesDetailsResponse?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        shared.GetAsync($"show:{id}", ct => LoadShowAsync(id, ct), cancellationToken);

    private async Task<SeriesDetailsResponse?> LoadShowAsync(int id, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"shows/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            shared.Invalidate($"episodes:{id}");
            shared.Invalidate($"show-cast:{id}");
            return null;
        }
        response.EnsureSuccessStatusCode();

        var show = await response.Content.ReadFromJsonAsync<TvMazeShow>(cancellationToken);
        if (show is null || show.Id != id || !DataIntegrity.Text(show.Name, 240, true)) throw new CatalogException("Invalid show response.");

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

    public Task<IReadOnlyCollection<EpisodeResponse>> GetEpisodesAsync(int seriesId, CancellationToken cancellationToken) =>
        shared.GetAsync($"episodes:{seriesId}", ct => LoadEpisodesAsync(seriesId, ct), cancellationToken);

    private async Task<IReadOnlyCollection<EpisodeResponse>> LoadEpisodesAsync(int seriesId, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"shows/{seriesId}/episodes", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            shared.Invalidate($"show:{seriesId}");
            shared.Invalidate($"show-cast:{seriesId}");
            throw new CatalogNotFoundException();
        }
        response.EnsureSuccessStatusCode();
        var episodes = await response.Content.ReadFromJsonAsync<List<TvMazeEpisode>>(cancellationToken)
            ?? throw new CatalogException("Null episodes.");
        if (episodes.Any(x => x is null || x.Id <= 0 || x.Season <= 0 || x.Number <= 0 || !DataIntegrity.Text(x.Name, 240, true)) ||
            episodes.Select(x => x.Id).Distinct().Count() != episodes.Count)
            throw new CatalogException("Invalid episodes.");

        var result = episodes.Select(x => new EpisodeResponse(
            x.Id,
            x.Season,
            x.Number,
            x.Name,
            CleanHtml(x.Summary),
            x.Airdate,
            x.Runtime,
            x.Image?.Medium)).ToArray();
        return result;
    }

    public Task<int> GetEpisodeSeriesIdAsync(int id, CancellationToken cancellationToken) =>
        shared.GetAsync($"episode-show:{id}", ct => LoadEpisodeSeriesIdAsync(id, ct), cancellationToken);

    private async Task<int> LoadEpisodeSeriesIdAsync(int id, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"episodes/{id}?embed=show", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            shared.Invalidate($"guest-cast:{id}");
            throw new CatalogNotFoundException();
        }
        response.EnsureSuccessStatusCode();
        using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = json.RootElement;
        if (root.ValueKind != System.Text.Json.JsonValueKind.Object || !root.TryGetProperty("id", out var eid) || eid.ValueKind != System.Text.Json.JsonValueKind.Number || !eid.TryGetInt32(out var actual) || actual != id ||
            !root.TryGetProperty("_embedded", out var embedded) || embedded.ValueKind != System.Text.Json.JsonValueKind.Object || !embedded.TryGetProperty("show", out var show) || show.ValueKind != System.Text.Json.JsonValueKind.Object ||
            !show.TryGetProperty("id", out var sid) || sid.ValueKind != System.Text.Json.JsonValueKind.Number || !sid.TryGetInt32(out var seriesId) || seriesId <= 0)
            throw new CatalogException("Invalid episode response.");
        return seriesId;
    }

    private static string? CleanHtml(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return WebUtility.HtmlDecode(HtmlTagRegex().Replace(value, string.Empty)).Trim();
    }

    [GeneratedRegex("<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();
}
