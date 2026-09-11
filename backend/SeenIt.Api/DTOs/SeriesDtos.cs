using System.ComponentModel.DataAnnotations;
using SeenIt.Api.Domain;

namespace SeenIt.Api.DTOs;

public sealed record SeriesSummaryResponse(
    int Id,
    string Name,
    string? PosterUrl,
    string? Summary,
    string? Premiered,
    string? Ended,
    double? Rating,
    IReadOnlyCollection<string> Genres);

public sealed record SeriesDetailsResponse(
    int Id,
    string Name,
    string? PosterUrl,
    string? OriginalPosterUrl,
    string? Summary,
    string? Premiered,
    string? Ended,
    string? Network,
    string? Status,
    double? Rating,
    IReadOnlyCollection<string> Genres);

public sealed record EpisodeResponse(
    int Id,
    int Season,
    int Number,
    string Name,
    string? Summary,
    string? AirDate,
    int? Runtime,
    string? ImageUrl);

public sealed record AddSeriesRequest(
    [Range(1, int.MaxValue)] int ExternalSeriesId,
    [Required, MaxLength(240)] string Name,
    string? PosterUrl,
    string? Summary,
    SeriesStatus Status);

public sealed record UpdateSeriesStatusRequest(SeriesStatus Status);

public sealed record UserSeriesResponse(
    Guid Id,
    int ExternalSeriesId,
    string Name,
    string? PosterUrl,
    string? Summary,
    SeriesStatus Status,
    DateTime AddedAtUtc,
    DateTime UpdatedAtUtc);
