using SeenIt.Api.DTOs.Profile;

namespace SeenIt.Api.Services;

public sealed record WatchedEpisodeTime(DateTime WatchedAtUtc, int? Runtime);

public static class WatchTimeCalculator
{
    public static WatchTimeResponse Calculate(
        IEnumerable<WatchedEpisodeTime> episodes, TimeZoneInfo timeZone, DateTime nowUtc)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(nowUtc, timeZone));
        var monday = today.AddDays(-((int)today.DayOfWeek + 6) % 7);
        var minutesByDay = new long[7];
        long total = 0;
        var missing = 0;

        foreach (var episode in episodes)
        {
            if (episode.Runtime is not > 0)
            {
                missing++;
                continue;
            }

            total += episode.Runtime.Value;
            var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(episode.WatchedAtUtc, DateTimeKind.Utc), timeZone));
            var index = date.DayNumber - monday.DayNumber;
            if (index is >= 0 and < 7 && date <= today)
                minutesByDay[index] += episode.Runtime.Value;
        }

        return new WatchTimeResponse(total, missing, today,
            Enumerable.Range(0, 7)
                .Select(index => new WatchTimeDayResponse(monday.AddDays(index), minutesByDay[index]))
                .ToArray());
    }
}
