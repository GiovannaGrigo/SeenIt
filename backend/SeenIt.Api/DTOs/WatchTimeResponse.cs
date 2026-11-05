namespace SeenIt.Api.DTOs.Profile;

public sealed record WatchTimeDayResponse(DateOnly Date, long Minutes);

public sealed record WatchTimeResponse(
    long TotalMinutes,
    int EpisodesWithoutRuntime,
    DateOnly Today,
    IReadOnlyList<WatchTimeDayResponse> Days);
