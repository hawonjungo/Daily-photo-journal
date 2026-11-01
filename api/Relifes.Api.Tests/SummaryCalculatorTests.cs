using Relifes.Api.Entities;
using Relifes.Api.Entries;
using Relifes.Api.Summaries;

namespace Relifes.Api.Tests;

public class SummaryCalculatorTests
{
    private static Entry NewEntry(
        string takenAtUtc,
        decimal? price = null,
        string? currency = null,
        string timeZone = "UTC",
        string? category = null,
        Place? place = null) => new()
    {
        TakenAt = DateTimeOffset.Parse(takenAtUtc).ToUniversalTime(),
        TimeZone = timeZone,
        ImageKey = "i",
        ThumbKey = "t",
        Price = price,
        Currency = currency,
        Category = category,
        Place = place,
        PlaceId = place?.Id,
    };

    private static Place NewPlace(string name) => new() { Name = name };

    private static DateOnly Date(string value) => DateOnly.Parse(value);

    [Theory]
    [InlineData("2026-11-02", "2026-11-02", "2026-11-08")] // Monday
    [InlineData("2026-11-04", "2026-11-02", "2026-11-08")] // Wednesday
    [InlineData("2026-11-08", "2026-11-02", "2026-11-08")] // Sunday belongs to the week it ends
    [InlineData("2026-12-30", "2026-12-28", "2027-01-03")] // Crosses a year
    public void Week_runs_monday_to_sunday(string date, string from, string to)
    {
        Assert.Equal((Date(from), Date(to)), SummaryCalculator.RangeFor(SummaryPeriod.Week, Date(date)));
    }

    [Theory]
    [InlineData("2026-11-15", "2026-11-01", "2026-11-30")]
    [InlineData("2026-12-31", "2026-12-01", "2026-12-31")]
    [InlineData("2028-02-10", "2028-02-01", "2028-02-29")] // Leap year
    [InlineData("2027-02-10", "2027-02-01", "2027-02-28")]
    public void Month_covers_the_calendar_month(string date, string from, string to)
    {
        Assert.Equal((Date(from), Date(to)), SummaryCalculator.RangeFor(SummaryPeriod.Month, Date(date)));
    }

    [Fact]
    public void Day_is_only_that_date()
    {
        Assert.Equal((Date("2026-11-15"), Date("2026-11-15")), SummaryCalculator.RangeFor(SummaryPeriod.Day, Date("2026-11-15")));
    }

    [Fact]
    public void Totals_are_exact_and_per_currency()
    {
        var result = SummaryCalculator.Calculate(SummaryPeriod.Day, Date("2026-11-15"),
        [
            NewEntry("2026-11-15T01:00:00Z", 0.1m, "AUD"),
            NewEntry("2026-11-15T02:00:00Z", 0.2m, "AUD"),
            NewEntry("2026-11-15T03:00:00Z", 45000m, "VND"),
            NewEntry("2026-11-15T04:00:00Z", 5000m, "VND"),
        ]);

        Assert.Equal(4, result.Count);
        Assert.Equal([new CurrencyTotal("AUD", 0.3m), new CurrencyTotal("VND", 50000m)], result.Totals);
    }

    [Fact]
    public void Entries_without_price_count_but_add_no_total()
    {
        var result = SummaryCalculator.Calculate(SummaryPeriod.Day, Date("2026-11-15"), [NewEntry("2026-11-15T01:00:00Z")]);

        Assert.Equal(1, result.Count);
        Assert.Empty(result.Totals);
    }

    [Fact]
    public void Zero_price_produces_a_zero_total()
    {
        var result = SummaryCalculator.Calculate(SummaryPeriod.Day, Date("2026-11-15"), [NewEntry("2026-11-15T01:00:00Z", 0m, "VND")]);

        Assert.Equal([new CurrencyTotal("VND", 0m)], result.Totals);
    }

    [Fact]
    public void No_entries_gives_an_empty_summary()
    {
        var result = SummaryCalculator.Calculate(SummaryPeriod.Month, Date("2026-11-15"), []);

        Assert.Equal(0, result.Count);
        Assert.Empty(result.Totals);
        Assert.Empty(result.TopPlaces);
        Assert.Empty(result.Categories);
    }

    [Fact]
    public void Day_is_decided_by_each_entrys_own_local_date()
    {
        var result = SummaryCalculator.Calculate(SummaryPeriod.Day, Date("2026-11-02"),
        [
            NewEntry("2026-11-01T18:00:00Z", 1m, "VND", "Asia/Ho_Chi_Minh"), // 2 Nov 01:00 local: included
            NewEntry("2026-11-02T18:00:00Z", 2m, "VND", "Asia/Ho_Chi_Minh"), // 3 Nov 01:00 local: excluded
            NewEntry("2026-11-02T02:00:00Z", 4m, "VND", "America/Los_Angeles"), // 1 Nov evening local: excluded
            NewEntry("2026-11-03T02:00:00Z", 8m, "VND", "America/Los_Angeles"), // 2 Nov evening local: included
        ]);

        Assert.Equal(2, result.Count);
        Assert.Equal([new CurrencyTotal("VND", 9m)], result.Totals);
    }

    [Fact]
    public void Daylight_saving_is_applied_for_the_date_of_the_photo()
    {
        // Sydney is UTC+10 in July and UTC+11 in January, so 13:30Z falls on different local dates.
        var winter = SummaryCalculator.Calculate(SummaryPeriod.Day, Date("2026-07-15"), [NewEntry("2026-07-15T13:30:00Z", 1m, "AUD", "Australia/Sydney")]);
        var summer = SummaryCalculator.Calculate(SummaryPeriod.Day, Date("2026-01-16"), [NewEntry("2026-01-15T13:30:00Z", 1m, "AUD", "Australia/Sydney")]);

        Assert.Equal(1, winter.Count);
        Assert.Equal(1, summer.Count);
    }

    [Fact]
    public void Week_includes_monday_and_sunday_but_not_the_days_beyond()
    {
        var result = SummaryCalculator.Calculate(SummaryPeriod.Week, Date("2026-11-04"),
        [
            NewEntry("2026-11-01T12:00:00Z", 1m, "VND"),
            NewEntry("2026-11-02T00:00:00Z", 2m, "VND"),
            NewEntry("2026-11-08T23:59:59Z", 4m, "VND"),
            NewEntry("2026-11-09T00:00:00Z", 8m, "VND"),
        ]);

        Assert.Equal([new CurrencyTotal("VND", 6m)], result.Totals);
    }

    [Fact]
    public void Top_places_are_ranked_by_visits_then_name_and_limited_to_five()
    {
        var places = Enumerable.Range(1, 7).Select(i => NewPlace($"Place {i}")).ToList();
        var entries = new List<Entry>();
        for (var i = 0; i < 3; i++)
        {
            entries.Add(NewEntry("2026-11-15T01:00:00Z", 10m, "VND", place: places[6]));
        }
        for (var i = 0; i < 2; i++)
        {
            entries.Add(NewEntry("2026-11-15T01:00:00Z", 1m, "AUD", place: places[1]));
        }
        entries.AddRange(places.Take(6).Where(p => p != places[1]).Select(p => NewEntry("2026-11-15T01:00:00Z", place: p)));
        entries.Add(NewEntry("2026-11-15T01:00:00Z"));

        var result = SummaryCalculator.Calculate(SummaryPeriod.Day, Date("2026-11-15"), entries);

        Assert.Equal(["Place 7", "Place 2", "Place 1", "Place 3", "Place 4"], result.TopPlaces.Select(p => p.Name));
        Assert.Equal(3, result.TopPlaces[0].Count);
        Assert.Equal([new CurrencyTotal("VND", 30m)], result.TopPlaces[0].Totals);
    }

    [Fact]
    public void Categories_are_grouped_with_uncategorized_last()
    {
        var result = SummaryCalculator.Calculate(SummaryPeriod.Day, Date("2026-11-15"),
        [
            NewEntry("2026-11-15T01:00:00Z", 10m, "VND"),
            NewEntry("2026-11-15T01:00:00Z", 10m, "VND"),
            NewEntry("2026-11-15T01:00:00Z", 10m, "VND"),
            NewEntry("2026-11-15T01:00:00Z", 5m, "VND", category: "Coffee"),
            NewEntry("2026-11-15T01:00:00Z", 7m, "VND", category: "Food"),
            NewEntry("2026-11-15T01:00:00Z", 1m, "AUD", category: "Food"),
        ]);

        Assert.Equal(["Food", "Coffee", null], result.Categories.Select(c => c.Category));
        Assert.Equal([new CurrencyTotal("AUD", 1m), new CurrencyTotal("VND", 7m)], result.Categories[0].Totals);
        Assert.Equal(3, result.Categories[2].Count);
    }
}
