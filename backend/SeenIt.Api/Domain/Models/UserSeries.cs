namespace SeenIt.Api.Domain;

public sealed class UserSeries
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public int ExternalSeriesId { get; set; }
    public required string Name { get; set; }
    public string? PosterUrl { get; set; }
    public string? Summary { get; set; }
    public SeriesStatus Status { get; set; } = SeriesStatus.InList;
    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
}
