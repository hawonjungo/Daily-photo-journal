namespace Relifes.Api.Entities;

public class Entry
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public DateTimeOffset TakenAt { get; set; }

    /// <summary>IANA time zone name, e.g. Australia/Sydney. Used to group entries by local date.</summary>
    public required string TimeZone { get; set; }

    public required string ImageKey { get; set; }
    public required string ThumbKey { get; set; }

    public decimal? Price { get; set; }

    /// <summary>ISO 4217 code, e.g. AUD, VND. Required whenever Price is set.</summary>
    public string? Currency { get; set; }

    public Guid? PlaceId { get; set; }
    public Place? Place { get; set; }

    public double? Lat { get; set; }
    public double? Lng { get; set; }

    public string? Category { get; set; }
    public string? Note { get; set; }
    public int? Rating { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
