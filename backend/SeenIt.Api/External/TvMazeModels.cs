using System.Text.Json.Serialization;

namespace SeenIt.Api.External;

internal sealed record TvMazeSearchResult(double Score, TvMazeShow Show);
internal sealed record TvMazeImage(string? Medium, string? Original);
internal sealed record TvMazeRating(double? Average);
internal sealed record TvMazeChannel(string? Name);

internal sealed record TvMazeShow(
    int Id,
    string Name,
    string? Summary,
    string? Premiered,
    string? Ended,
    string? Status,
    IReadOnlyCollection<string>? Genres,
    TvMazeImage? Image,
    TvMazeRating? Rating,
    TvMazeChannel? Network,
    [property: JsonPropertyName("webChannel")] TvMazeChannel? WebChannel);

internal sealed record TvMazeEpisode(
    int Id,
    int Season,
    int Number,
    string Name,
    string? Summary,
    string? Airdate,
    int? Runtime,
    TvMazeImage? Image);
