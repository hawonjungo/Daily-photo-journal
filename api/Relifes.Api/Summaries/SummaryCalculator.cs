using Relifes.Api.Entities;
using Relifes.Api.Entries;

namespace Relifes.Api.Summaries;

public record PlaceSummary(Guid PlaceId, string Name, int Count, IReadOnlyList<CurrencyTotal> Totals);

public record CategorySummary(string? Category, int Count, IReadOnlyList<CurrencyTotal> Totals);

public record SummaryResponse(
    string Period,
    DateOnly From,
    DateOnly To,
    int Count,
    IReadOnlyList<CurrencyTotal> Totals,
    IReadOnlyList<PlaceSummary> TopPlaces,
    IReadOnlyList<CategorySummary> Categories);

public static class SummaryPeriod
{
    public const string Day = "day";
    public const string Week = "week";
    public const string Month = "month";

    public static bool IsValid(string? period) => period is Day or Week or Month;
}

public static class SummaryCalculator
{
    public const int MaxTopPlaces = 5;

    /// <summary>The day, the Monday-to-Sunday week or the calendar month containing the date.</summary>
    public static (DateOnly From, DateOnly To) RangeFor(string period, DateOnly date)
    {
        switch (period)
        {
            case SummaryPeriod.Day:
                return (date, date);
            case SummaryPeriod.Week:
                var monday = date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
                return (monday, monday.AddDays(6));
            case SummaryPeriod.Month:
                var first = new DateOnly(date.Year, date.Month, 1);
                return (first, first.AddMonths(1).AddDays(-1));
            default:
                throw new ArgumentOutOfRangeException(nameof(period), period, "Unknown summary period.");
        }
    }

    /// <summary>Entries need their Place loaded. Those whose local date falls outside the period are ignored.</summary>
    public static SummaryResponse Calculate(string period, DateOnly date, IEnumerable<Entry> entries)
    {
        var (from, to) = RangeFor(period, date);
        var inPeriod = entries
            .Where(e =>
            {
                var localDate = LocalDates.Of(e.TakenAt, e.TimeZone);
                return localDate >= from && localDate <= to;
            })
            .ToList();

        var topPlaces = inPeriod
            .Where(e => e.PlaceId is not null && e.Place is not null)
            .GroupBy(e => e.PlaceId!.Value)
            .Select(group => new PlaceSummary(group.Key, group.First().Place!.Name, group.Count(), CurrencyTotal.For(group)))
            .OrderByDescending(place => place.Count)
            .ThenBy(place => place.Name, StringComparer.Ordinal)
            .Take(MaxTopPlaces)
            .ToList();

        var categories = inPeriod
            .GroupBy(e => e.Category)
            .Select(group => new CategorySummary(group.Key, group.Count(), CurrencyTotal.For(group)))
            .OrderBy(category => category.Category is null)
            .ThenByDescending(category => category.Count)
            .ThenBy(category => category.Category, StringComparer.Ordinal)
            .ToList();

        return new SummaryResponse(period, from, to, inPeriod.Count, CurrencyTotal.For(inPeriod), topPlaces, categories);
    }
}
