namespace Relifes.Api.Entries;

public record TimelineResponse(DateOnly From, DateOnly To, IReadOnlyList<TimelineDay> Days);

public record TimelineDay(DateOnly Date, int Count, IReadOnlyList<CurrencyTotal> Totals, IReadOnlyList<TimelineEntry> Entries);

public record TimelineEntry(
    Guid Id,
    DateTimeOffset TakenAt,
    string TimeZone,
    string ImageUrl,
    string ThumbUrl,
    decimal? Price,
    string? Currency,
    Guid? PlaceId,
    string? PlaceName,
    string? Category,
    string? Note,
    int? Rating);
