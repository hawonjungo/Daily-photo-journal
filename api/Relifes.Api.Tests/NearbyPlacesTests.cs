using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Relifes.Api.Entities;
using Relifes.Api.Places;

namespace Relifes.Api.Tests;

[Collection(ApiCollection.Name)]
public class NearbyPlacesTests(ApiFixture api) : IAsyncLifetime
{
    private const double MetersPerDegreeLat = 111_320;

    // Each test works at its own spot, because Google places are shared between users.
    private static int _nextSpot;

    private (Guid Id, string ApiKey) _user;
    private double _lat;
    private double _lng;

    public async Task InitializeAsync()
    {
        _user = await api.CreateUserAsync();
        var spot = Interlocked.Increment(ref _nextSpot);
        _lat = -33 + spot * 0.05;
        _lng = 151 + spot * 0.05;

        api.PlacesClient.CallCount = 0;
        api.PlacesClient.Respond = (_, _) => [];
        await api.WithDbAsync(db => db.GoogleApiUsage.ExecuteDeleteAsync());
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<HttpResponseMessage> GetAsync(string query, string? apiKey = null)
    {
        using var client = api.CreateClient();
        if (apiKey != "")
        {
            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey ?? _user.ApiKey);
        }

        return await client.GetAsync($"/api/places/nearby?{query}");
    }

    private async Task<NearbyPlacesResponse> FindAsync(double lat, double lng, string? apiKey = null)
    {
        var response = await GetAsync(FormattableString.Invariant($"lat={lat}&lng={lng}"), apiKey);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<NearbyPlacesResponse>())!;
    }

    private double North(double meters) => _lat + meters / MetersPerDegreeLat;

    private async Task<Place> AddPlaceAsync(string name, double lat, double lng, Guid? createdBy = null, string? googleId = null)
    {
        var place = new Place { Name = name, Lat = lat, Lng = lng, CreatedByUserId = createdBy ?? _user.Id, GooglePlaceId = googleId };
        await api.WithDbAsync(async db =>
        {
            db.Places.Add(place);
            await db.SaveChangesAsync();
        });
        return place;
    }

    private static ExternalPlace Google(string name, double lat, double lng) =>
        new($"google-{Guid.NewGuid()}", name, $"{name} address", lat, lng);

    private async Task<int> TodayGoogleCountAsync()
    {
        var count = 0;
        await api.WithDbAsync(async db => count = await db.GoogleApiUsage.SumAsync(u => u.CallCount));
        return count;
    }

    [Fact]
    public async Task Requires_api_key()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync("lat=1&lng=1", apiKey: "")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync("lat=1&lng=1", apiKey: "rl_wrong")).StatusCode);
    }

    [Theory]
    [InlineData("lng=1")]
    [InlineData("lat=1")]
    [InlineData("lat=91&lng=1")]
    [InlineData("lat=1&lng=181")]
    [InlineData("lat=abc&lng=1")]
    [InlineData("lat=NaN&lng=1")]
    public async Task Invalid_coordinates_return_400_without_calling_google(string query)
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await GetAsync(query)).StatusCode);
        Assert.Equal(0, api.PlacesClient.CallCount);
    }

    [Fact]
    public async Task Known_place_within_80_m_is_returned_without_calling_google()
    {
        var place = await AddPlaceAsync("Nearby", North(60), _lng);

        var result = await FindAsync(_lat, _lng);

        Assert.Equal(NearbySource.Database, result.Source);
        var found = Assert.Single(result.Places);
        Assert.Equal(place.Id, found.Id);
        Assert.InRange(found.DistanceMeters, 58, 62);
        Assert.Equal(0, api.PlacesClient.CallCount);
        Assert.Equal(0, await TodayGoogleCountAsync());
    }

    [Fact]
    public async Task Place_beyond_80_m_does_not_count_and_google_is_called()
    {
        await AddPlaceAsync("Too far", North(100), _lng);

        var result = await FindAsync(_lat, _lng);

        Assert.Equal(1, api.PlacesClient.CallCount);
        Assert.Empty(result.Places);
    }

    [Fact]
    public async Task Corner_of_the_search_box_beyond_80_m_is_excluded()
    {
        // Inside the square the database searches, but about 99 m away in a straight line.
        await AddPlaceAsync("Corner", North(70), _lng + 70 / (MetersPerDegreeLat * Math.Cos(double.DegreesToRadians(_lat))));

        var result = await FindAsync(_lat, _lng);

        Assert.Empty(result.Places);
        Assert.Equal(1, api.PlacesClient.CallCount);
    }

    [Fact]
    public async Task Results_are_sorted_by_distance_and_limited_to_10()
    {
        for (var i = 0; i < 12; i++)
        {
            await AddPlaceAsync($"Place {i}", North(70 - i * 5), _lng);
        }

        var result = await FindAsync(_lat, _lng);

        Assert.Equal(10, result.Places.Count);
        Assert.Equal(result.Places.OrderBy(p => p.DistanceMeters).Select(p => p.Id), result.Places.Select(p => p.Id));
        Assert.Equal("Place 11", result.Places[0].Name);
    }

    [Fact]
    public async Task Another_users_manual_place_is_private_but_google_places_are_shared()
    {
        var other = await api.CreateUserAsync();
        await AddPlaceAsync("Their manual place", North(10), _lng, other.Id);
        await AddPlaceAsync("Shared google place", North(20), _lng, other.Id, $"google-{Guid.NewGuid()}");

        var result = await FindAsync(_lat, _lng);

        var found = Assert.Single(result.Places);
        Assert.Equal("Shared google place", found.Name);
    }

    [Fact]
    public async Task Google_results_are_saved_so_the_next_request_does_not_call_google()
    {
        var (lat, lng) = (_lat, _lng);
        api.PlacesClient.Respond = (_, _) => [Google("Far", lat + 50 / MetersPerDegreeLat, lng), Google("Close", lat + 10 / MetersPerDegreeLat, lng)];

        var first = await FindAsync(_lat, _lng);

        Assert.Equal(NearbySource.Google, first.Source);
        Assert.Equal(["Close", "Far"], first.Places.Select(p => p.Name));
        Assert.Equal(1, await TodayGoogleCountAsync());

        var second = await FindAsync(_lat, _lng);

        Assert.Equal(NearbySource.Database, second.Source);
        Assert.Equal(first.Places.Select(p => p.Id), second.Places.Select(p => p.Id));
        Assert.Equal(1, api.PlacesClient.CallCount);
    }

    [Fact]
    public async Task Returned_id_can_be_used_as_a_saved_place()
    {
        var (lat, lng) = (_lat, _lng);
        api.PlacesClient.Respond = (_, _) => [Google("Cafe", lat, lng)];

        var result = await FindAsync(_lat, _lng);

        Place? saved = null;
        var id = result.Places[0].Id;
        await api.WithDbAsync(async db => saved = await db.Places.SingleAsync(p => p.Id == id));
        Assert.Equal(_user.Id, saved!.CreatedByUserId);
        Assert.StartsWith("google-", saved.GooglePlaceId);
        Assert.Equal("Cafe address", saved.Address);
    }

    [Fact]
    public async Task Google_place_already_saved_elsewhere_is_reused_not_duplicated()
    {
        var googleId = $"google-{Guid.NewGuid()}";
        // Saved earlier with a location that has since changed, so the database lookup does not find it here.
        var existing = await AddPlaceAsync("Old name", _lat + 1, _lng, googleId: googleId);
        var (lat, lng) = (_lat, _lng);
        api.PlacesClient.Respond = (_, _) => [new ExternalPlace(googleId, "New name", null, lat, lng)];

        var result = await FindAsync(_lat, _lng);

        var found = Assert.Single(result.Places);
        Assert.Equal(existing.Id, found.Id);
        Assert.Equal("New name", found.Name);
    }

    [Fact]
    public async Task Empty_google_result_is_cached()
    {
        var first = await FindAsync(_lat, _lng);
        var second = await FindAsync(_lat, _lng);

        Assert.Equal(NearbyFallback.NoResults, first.FallbackReason);
        Assert.Equal(NearbyFallback.NoResults, second.FallbackReason);
        Assert.Equal(1, api.PlacesClient.CallCount);
    }

    [Fact]
    public async Task At_the_daily_cap_google_is_not_called_and_clients_fall_back_to_manual()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await api.WithDbAsync(async db =>
        {
            db.GoogleApiUsage.Add(new GoogleApiUsage { Date = today, CallCount = ApiFixture.DailyGoogleCap });
            await db.SaveChangesAsync();
        });

        var result = await FindAsync(_lat, _lng);

        Assert.Equal(0, api.PlacesClient.CallCount);
        Assert.Empty(result.Places);
        Assert.Equal(NearbySource.None, result.Source);
        Assert.Equal(NearbyFallback.DailyLimitReached, result.FallbackReason);
    }

    [Fact]
    public async Task Known_places_are_still_returned_at_the_daily_cap()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await api.WithDbAsync(async db =>
        {
            db.GoogleApiUsage.Add(new GoogleApiUsage { Date = today, CallCount = ApiFixture.DailyGoogleCap });
            await db.SaveChangesAsync();
        });
        await AddPlaceAsync("Known", _lat, _lng);

        var result = await FindAsync(_lat, _lng);

        Assert.Equal(NearbySource.Database, result.Source);
        Assert.Single(result.Places);
    }

    [Fact]
    public async Task Concurrent_requests_never_exceed_the_cap()
    {
        var responses = await Task.WhenAll(Enumerable.Range(1, 20)
            .Select(i => FindAsync(_lat + i * 0.01, _lng)));

        Assert.Equal(ApiFixture.DailyGoogleCap, api.PlacesClient.CallCount);
        Assert.Equal(ApiFixture.DailyGoogleCap, await TodayGoogleCountAsync());
        Assert.Equal(20 - ApiFixture.DailyGoogleCap, responses.Count(r => r.FallbackReason == NearbyFallback.DailyLimitReached));
    }

    [Fact]
    public async Task Yesterdays_usage_does_not_count_today()
    {
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        await api.WithDbAsync(async db =>
        {
            db.GoogleApiUsage.Add(new GoogleApiUsage { Date = yesterday, CallCount = ApiFixture.DailyGoogleCap });
            await db.SaveChangesAsync();
        });

        await FindAsync(_lat, _lng);

        Assert.Equal(1, api.PlacesClient.CallCount);
    }

    [Fact]
    public async Task Google_failure_falls_back_to_manual_and_is_not_cached()
    {
        api.PlacesClient.Respond = (_, _) => throw new HttpRequestException("Simulated outage");

        var failed = await FindAsync(_lat, _lng);

        Assert.Equal(NearbyFallback.GoogleError, failed.FallbackReason);

        var (lat, lng) = (_lat, _lng);
        api.PlacesClient.Respond = (_, _) => [Google("Recovered", lat, lng)];

        var recovered = await FindAsync(_lat, _lng);

        Assert.Equal(NearbySource.Google, recovered.Source);
    }
}
