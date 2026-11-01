using System.Net;
using System.Net.Http.Json;
using Relifes.Api.Entities;
using Relifes.Api.Entries;
using Relifes.Api.Summaries;

namespace Relifes.Api.Tests;

[Collection(ApiCollection.Name)]
public class SummaryEndpointTests(ApiFixture api) : IAsyncLifetime
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

        return await client.GetAsync($"/api/summary{query}");
    }

    private async Task AddEntryAsync(string takenAtUtc, decimal price, string timeZone, Guid userId, Place? place = null, string? category = null)
    {
        await api.WithDbAsync(async db =>
        {
            db.Entries.Add(new Entry
            {
                UserId = userId,
                TakenAt = DateTimeOffset.Parse(takenAtUtc).ToUniversalTime(),
                TimeZone = timeZone,
                ImageKey = "i",
                ThumbKey = "t",
                Price = price,
                Currency = "VND",
                Place = place,
                Category = category,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task Requires_api_key()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync("?period=day&date=2026-11-01", apiKey: "")).StatusCode);
    }

    [Theory]
    [InlineData("?date=2026-11-01")]
    [InlineData("?period=year&date=2026-11-01")]
    [InlineData("?period=day")]
    [InlineData("?period=day&date=01-11-2026")]
    public async Task Invalid_query_returns_400(string query)
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await GetAsync(query)).StatusCode);
    }

    [Fact]
    public async Task Summarises_only_this_users_entries_in_the_local_period()
    {
        var other = await api.CreateUserAsync();
        var place = new Place { Name = "Pho 24", CreatedByUserId = _user.Id };

        // Sunday 8 Nov 23:30 local, stored as the same UTC date.
        await AddEntryAsync("2026-11-08T16:30:00Z", 10m, "Asia/Ho_Chi_Minh", _user.Id, place, "Food");
        // Monday 9 Nov 00:30 local, although the UTC date is still 8 Nov: next week.
        await AddEntryAsync("2026-11-08T17:30:00Z", 100m, "Asia/Ho_Chi_Minh", _user.Id);
        // Monday 2 Nov 00:30 local, although the UTC date is 1 Nov: this week.
        await AddEntryAsync("2026-11-01T17:30:00Z", 1m, "Asia/Ho_Chi_Minh", _user.Id);
        await AddEntryAsync("2026-11-05T01:00:00Z", 1000m, "Asia/Ho_Chi_Minh", other.Id);

        var response = await GetAsync("?period=week&date=2026-11-04");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = (await response.Content.ReadFromJsonAsync<SummaryResponse>())!;
        Assert.Equal(new DateOnly(2026, 11, 2), summary.From);
        Assert.Equal(new DateOnly(2026, 11, 8), summary.To);
        Assert.Equal(2, summary.Count);
        Assert.Equal([new CurrencyTotal("VND", 11m)], summary.Totals);
        Assert.Equal("Pho 24", Assert.Single(summary.TopPlaces).Name);
        Assert.Equal(["Food", null], summary.Categories.Select(c => c.Category));
    }
}
