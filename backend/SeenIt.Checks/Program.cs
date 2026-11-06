using SeenIt.Api.Services;

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
var now = new DateTime(2026, 11, 5, 15, 0, 0, DateTimeKind.Utc);
var empty = WatchTimeCalculator.Calculate([], zone, now);
Check(empty.TotalMinutes == 0 && empty.Days.Count == 7 && empty.Days.All(day => day.Minutes == 0), "Empty history must have seven zero days.");
Check(empty.Days[0].Date == new DateOnly(2026, 11, 2) && empty.Days[6].Date == new DateOnly(2026, 11, 8), "Week must run Monday to Sunday.");

var result = WatchTimeCalculator.Calculate([
    new(new DateTime(2026, 11, 2, 2, 59, 0), 30), // Sunday locally; total only.
    new(new DateTime(2026, 11, 2, 3, 0, 0), 60), // Monday locally.
    new(new DateTime(2026, 11, 5, 2, 59, 0), 20), // Wednesday locally.
    new(new DateTime(2026, 11, 5, 3, 0, 0), 45), // Thursday locally.
    new(new DateTime(2026, 11, 5, 4, 0, 0), 15), // Same day must sum.
    new(new DateTime(2026, 10, 1), 120), // Old history contributes to all-time total.
    new(now, null), new(now, 0), new(now, -1),
], zone, now);
Check(result.TotalMinutes == 290, "Total must sum actual positive runtimes across all history.");
Check(result.EpisodesWithoutRuntime == 3, "Unknown or invalid runtimes must be reported, not estimated.");
Check(result.Days.Select(day => day.Minutes).SequenceEqual(new long[] { 60, 0, 20, 60, 0, 0, 0 }), "Daily activity must respect local midnight and sum episodes.");
Check(result.Today == new DateOnly(2026, 11, 5), "Current day must use the requested timezone.");

var sunday = WatchTimeCalculator.Calculate([], zone, new DateTime(2026, 11, 2, 2, 59, 0, DateTimeKind.Utc));
Check(sunday.Today == new DateOnly(2026, 11, 1) && sunday.Days[0].Date == new DateOnly(2026, 10, 26), "Sunday must belong to the preceding Monday's week.");
var big = WatchTimeCalculator.Calculate([new(now, int.MaxValue), new(now, int.MaxValue)], zone, now);
Check(big.TotalMinutes == 2L * int.MaxValue && big.Days[3].Minutes == big.TotalMinutes, "Aggregations must avoid integer overflow.");
Console.WriteLine("Watch time checks passed: totals, empty history, unknown durations, timezone boundaries, week rollover and overflow.");
await AnimeClassificationChecks.RunAsync();
