using System.Net;
using System.Net.Http.Json;
using Relifes.Api.Entities;
using Relifes.Api.Entries;

namespace Relifes.Api.Tests;

[Collection(ApiCollection.Name)]
public class TimelineTests(ApiFixture api) : IAsyncLifetime
{
    private (Guid Id, string ApiKey) _user;

    public async Task InitializeAsync() => _user = await api.CreateUserAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<HttpResponseMessage> GetAsync(string query, string? apiKey = null)
    {
        using var client = api.CreateClient();
        if (apiKey != "")
        {
            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey ?? _user.ApiKey);
        }

        return await client.GetAsync($"/api/entries{query}");
    }

    private async Task<TimelineResponse> GetTimelineAsync(string from, string to)
    {
        var response = await GetAsync($"?from={from}&to={to}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TimelineResponse>())!;
    }

    private async Task<Entry> AddEntryAsync(
        string takenAtUtc,
        string timeZone = "UTC",
        decimal? price = null,
        string? currency = null,
        Guid? userId = null,
        Place? place = null)
    {
        var entry = new Entry
        {
            UserId = userId ?? _user.Id,
            TakenAt = DateTimeOffset.Parse(takenAtUtc).ToUniversalTime(),
            TimeZone = timeZone,
            ImageKey = "full.webp",
            ThumbKey = "thumb.webp",
            Price = price,
            Currency = currency,
            Place = place,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        await api.WithDbAsync(async db =>
        {
            db.Entries.Add(entry);
            await db.SaveChangesAsync();
        });
        return entry;
    }

    [Fact]
    public async Task Requires_api_key()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync("", apiKey: "")).StatusCode);
    }

    [Theory]
    [InlineData("?from=01/11/2026", "from")]
    [InlineData("?to=nonsense", "to")]
    [InlineData("?from=2026-11-02&to=2026-11-01", "from")]
    [InlineData("?from=2024-01-01&to=2026-01-01", "from")]
    public async Task Invalid_range_returns_400(string query, string field)
    {
        var response = await GetAsync(query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(field, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Entries_are_grouped_by_their_local_date_not_the_utc_date()
    {
        // Same UTC day, but 18:00Z is already 2 November in Ho Chi Minh City (UTC+7).
        var evening = await AddEntryAsync("2026-11-01T14:30:00Z", "Asia/Ho_Chi_Minh");
        var pastMidnight = await AddEntryAsync("2026-11-01T18:00:00Z", "Asia/Ho_Chi_Minh");

        var timeline = await GetTimelineAsync("2026-11-01", "2026-11-02");

        Assert.Equal([new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 1)], timeline.Days.Select(d => d.Date));
        Assert.Equal(pastMidnight.Id, Assert.Single(timeline.Days[0].Entries).Id);
        Assert.Equal(evening.Id, Assert.Single(timeline.Days[1].Entries).Id);
    }

    [Fact]
    public async Task Range_edges_use_the_local_date()
    {
        // Local date is 2 November in Sydney (UTC+11), even though the UTC date is 1 November.
        await AddEntryAsync("2026-11-01T14:00:00Z", "Australia/Sydney");
        // Local date is 31 October in Los Angeles, even though the UTC date is 1 November.
        await AddEntryAsync("2026-11-01T01:00:00Z", "America/Los_Angeles");

        var timeline = await GetTimelineAsync("2026-11-01", "2026-11-01");

        Assert.Empty(timeline.Days);
    }

    [Fact]
    public async Task Day_totals_are_per_currency_and_never_mixed()
    {
        await AddEntryAsync("2026-10-10T01:00:00Z", price: 0.1m, currency: "AUD");
        await AddEntryAsync("2026-10-10T02:00:00Z", price: 0.2m, currency: "AUD");
        await AddEntryAsync("2026-10-10T03:00:00Z", price: 45000m, currency: "VND");
        await AddEntryAsync("2026-10-10T04:00:00Z");

        var day = Assert.Single((await GetTimelineAsync("2026-10-10", "2026-10-10")).Days);

        Assert.Equal(4, day.Count);
        Assert.Equal([new CurrencyTotal("AUD", 0.3m), new CurrencyTotal("VND", 45000m)], day.Totals);
    }

    [Fact]
    public async Task Entries_are_newest_first_with_signed_image_urls_and_place_name()
    {
        var place = new Place { Name = "Pho 24", CreatedByUserId = _user.Id };
        await AddEntryAsync("2026-09-01T01:00:00Z");
        var latest = await AddEntryAsync("2026-09-01T09:00:00Z", place: place);

        var day = Assert.Single((await GetTimelineAsync("2026-09-01", "2026-09-01")).Days);

        Assert.Equal(2, day.Entries.Count);
        Assert.Equal(latest.Id, day.Entries[0].Id);
        Assert.Equal("Pho 24", day.Entries[0].PlaceName);
        Assert.Equal("https://fake-storage/full.webp?signed=true", day.Entries[0].ImageUrl);
        Assert.Equal("https://fake-storage/thumb.webp?signed=true", day.Entries[0].ThumbUrl);
    }

    [Fact]
    public async Task Other_users_entries_are_not_returned()
    {
        var other = await api.CreateUserAsync();
        await AddEntryAsync("2026-08-01T01:00:00Z", userId: other.Id);

        Assert.Empty((await GetTimelineAsync("2026-08-01", "2026-08-01")).Days);
    }

    [Fact]
    public async Task Without_a_range_recent_entries_are_returned()
    {
        var recent = await AddEntryAsync(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"));
        var old = await AddEntryAsync(DateTimeOffset.UtcNow.AddDays(-90).ToString("O"));

        var response = await GetAsync("");

        var timeline = (await response.Content.ReadFromJsonAsync<TimelineResponse>())!;
        var ids = timeline.Days.SelectMany(d => d.Entries).Select(e => e.Id).ToList();
        Assert.Contains(recent.Id, ids);
        Assert.DoesNotContain(old.Id, ids);
    }
}
