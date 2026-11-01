using Relifes.Api.Entities;

namespace Relifes.Api.Entries;

public record EntryResponse(
    Guid Id,
    DateTimeOffset TakenAt,
    string TimeZone,
    string ImageKey,
    string ThumbKey,
    decimal? Price,
    string? Currency,
    Guid? PlaceId,
    double? Lat,
    double? Lng,
    string? Category,
    string? Note,
    int? Rating,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static EntryResponse From(Entry entry) => new(
        entry.Id,
        entry.TakenAt,
        entry.TimeZone,
        entry.ImageKey,
        entry.ThumbKey,
        entry.Price,
        entry.Currency,
        entry.PlaceId,
        entry.Lat,
        entry.Lng,
        entry.Category,
        entry.Note,
        entry.Rating,
        entry.CreatedAt,
        entry.UpdatedAt);
}
