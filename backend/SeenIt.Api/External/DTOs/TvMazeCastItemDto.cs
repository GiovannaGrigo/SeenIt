using System.Text.Json.Serialization;

namespace SeenIt.Api.External.DTOs;

public sealed class TvMazeCastItemDto
{
    [JsonPropertyName("person")]
    public TvMazePersonDto? Person { get; init; }

    [JsonPropertyName("character")]
    public TvMazeCharacterDto? Character { get; init; }
}

public sealed class TvMazePersonDto
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("image")]
    public TvMazeImageDto? Image { get; init; }
}

public sealed class TvMazeCharacterDto
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("image")]
    public TvMazeImageDto? Image { get; init; }
}

public sealed class TvMazeImageDto
{
    [JsonPropertyName("medium")]
    public string? Medium { get; init; }

    [JsonPropertyName("original")]
    public string? Original { get; init; }
}