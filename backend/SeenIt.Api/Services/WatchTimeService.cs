using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SeenIt.Api.Data;
using SeenIt.Api.DTOs.Profile;
using SeenIt.Api.External;

namespace SeenIt.Api.Services;

public sealed class WatchTimeService(AppDbContext database, TvMazeClient tvMazeClient, IMemoryCache cache)
{
    public async Task<WatchTimeResponse> GetAsync(Guid userId, TimeZoneInfo timeZone, CancellationToken cancellationToken)
    {
        var watched = await database.EpisodiosAssistidos.AsNoTracking()
            .Where(episode => episode.UserId == userId)
            .Select(episode => new { episode.ExternalSeriesId, episode.ExternalEpisodeId, episode.AssistidoEmUtc })
            .ToListAsync(cancellationToken);

        // Cache catalog durations per series, never user activity or totals.
        using var concurrency = new SemaphoreSlim(4);
        var durations = await Task.WhenAll(watched.Select(episode => episode.ExternalSeriesId).Distinct()
            .Select(async seriesId =>
            {
                await concurrency.WaitAsync(cancellationToken);
                try
                {
                    var runtimes = await cache.GetOrCreateAsync($"watch-time:series:{seriesId}", async entry =>
                    {
                        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);
                        var episodes = await tvMazeClient.GetEpisodesAsync(seriesId, cancellationToken);
                        return episodes.ToDictionary(episode => episode.Id, episode => episode.Runtime);
                    });
                    return (SeriesId: seriesId, Runtimes: runtimes!);
                }
                finally { concurrency.Release(); }
            }));

        var bySeries = durations.ToDictionary(item => item.SeriesId, item => item.Runtimes);
        return WatchTimeCalculator.Calculate(watched.Select(episode => new WatchedEpisodeTime(
            episode.AssistidoEmUtc,
            bySeries[episode.ExternalSeriesId].GetValueOrDefault(episode.ExternalEpisodeId))),
            timeZone, DateTime.UtcNow);
    }
}
