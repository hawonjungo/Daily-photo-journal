namespace Relifes.Api.Entries;

public static class LocalDates
{
    public static DateOnly Of(DateTimeOffset takenAt, string timeZone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(takenAt, TimeZoneInfo.FindSystemTimeZoneById(timeZone)).DateTime);

    /// <summary>
    /// A UTC range certain to contain every entry whose local date is in the range, whatever its time zone.
    /// UTC offsets run from -12 to +14 hours; callers filter the rows exactly with <see cref="Of"/>.
    /// </summary>
    public static (DateTimeOffset Start, DateTimeOffset End) UtcWindow(DateOnly from, DateOnly to) => (
        new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddHours(-14),
        new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddHours(12));
}
