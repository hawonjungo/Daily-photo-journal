using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Relifes.Api.Entities;
using Relifes.Api.Entries;

namespace Relifes.Api.Tests;

[Collection(ApiCollection.Name)]
public class CreateEntryTests(ApiFixture api) : IAsyncLifetime
{
    private (Guid Id, string ApiKey) _user;

    public async Task InitializeAsync()
    {
        api.Storage.FailUploadsEndingWith = null;
        _user = await api.CreateUserAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<HttpResponseMessage> PostAsync(
        Dictionary<string, string>? fields = null,
        byte[]? image = null,
        string? apiKey = null)
    {
        using var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey ?? _user.ApiKey);

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(image ?? TestImages.Jpeg(800, 600));
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "image", "photo.jpg");
        foreach (var (name, value) in fields ?? [])
        {
            form.Add(new StringContent(value), name);
        }

        return await client.PostAsync("/api/entries", form);
    }

    private async Task<Entry?> LoadEntryAsync(Guid id)
    {
        Entry? entry = null;
        await api.WithDbAsync(async db => entry = await db.Entries.Include(e => e.Place).SingleOrDefaultAsync(e => e.Id == id));
        return entry;
    }

    private async Task<int> CountEntriesAsync()
    {
        var count = 0;
        await api.WithDbAsync(async db => count = await db.Entries.CountAsync(e => e.UserId == _user.Id));
        return count;
    }

    private bool StorageHasObjectsForUser() =>
        api.Storage.Objects.Keys.Any(k => k.StartsWith($"users/{_user.Id}/"));

    [Fact]
    public async Task Missing_api_key_returns_401()
    {
        using var client = api.CreateClient();
        using var form = new MultipartFormDataContent { { new ByteArrayContent(TestImages.Jpeg(10, 10)), "image", "a.jpg" } };

        var response = await client.PostAsync("/api/entries", form);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_api_key_returns_401()
    {
        var response = await PostAsync(apiKey: "rl_wrong");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Valid_upload_stores_images_and_entry()
    {
        var response = await PostAsync(new()
        {
            ["price"] = "12.50",
            ["currency"] = "aud",
            ["lat"] = "-33.8688",
            ["lng"] = "151.2093",
            ["placeName"] = "Bun Bo Hue",
            ["takenAt"] = "2026-11-01T12:30:00+11:00",
            ["timeZone"] = "Australia/Sydney",
        }, TestImages.Jpeg(3000, 2000, exifOrientation: 6));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<EntryResponse>();
        Assert.Equal($"/api/entries/{body!.Id}", response.Headers.Location?.ToString());

        var entry = await LoadEntryAsync(body.Id);
        Assert.NotNull(entry);
        Assert.Equal(_user.Id, entry.UserId);
        Assert.Equal(12.50m, entry.Price);
        Assert.Equal("AUD", entry.Currency);
        Assert.Equal(-33.8688, entry.Lat);
        Assert.Equal(151.2093, entry.Lng);
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 1, 30, 0, TimeSpan.Zero), entry.TakenAt);
        Assert.Equal(TimeSpan.Zero, entry.TakenAt.Offset);
        Assert.Equal("Australia/Sydney", entry.TimeZone);
        Assert.Equal("Bun Bo Hue", entry.Place!.Name);
        Assert.Equal(_user.Id, entry.Place.CreatedByUserId);
        Assert.Null(entry.Place.GooglePlaceId);

        using var full = SkiaSharp.SKBitmap.Decode(api.Storage.Objects[entry.ImageKey]);
        using var thumbnail = SkiaSharp.SKBitmap.Decode(api.Storage.Objects[entry.ThumbKey]);
        Assert.Equal((1067, 1600), (full.Width, full.Height));
        Assert.Equal((267, 400), (thumbnail.Width, thumbnail.Height));
    }

    [Fact]
    public async Task Image_alone_is_enough_and_defaults_apply()
    {
        var before = DateTimeOffset.UtcNow;

        var response = await PostAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<EntryResponse>();
        var entry = await LoadEntryAsync(body!.Id);
        Assert.Null(entry!.Price);
        Assert.Null(entry.Currency);
        Assert.Null(entry.PlaceId);
        Assert.Equal("UTC", entry.TimeZone);
        Assert.True(entry.TakenAt >= before.AddSeconds(-1));
    }

    [Theory]
    [InlineData("50000", 50000)]
    [InlineData("12.5", 12.5)]
    [InlineData("0", 0)]
    public async Task Valid_prices_are_stored_exactly(string price, decimal expected)
    {
        var response = await PostAsync(new() { ["price"] = price, ["currency"] = "VND" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<EntryResponse>();
        Assert.Equal(expected, (await LoadEntryAsync(body!.Id))!.Price);
    }

    [Theory]
    [InlineData("price", "12,5")]
    [InlineData("price", "50.000")]
    [InlineData("price", "1,000")]
    [InlineData("price", "-1")]
    [InlineData("price", "abc")]
    [InlineData("currency", "AU")]
    [InlineData("currency", "A1D")]
    [InlineData("lat", "91")]
    [InlineData("lat", "NaN")]
    [InlineData("lng", "181")]
    [InlineData("placeId", "not-a-guid")]
    [InlineData("takenAt", "2026-11-01T12:30:00")]
    [InlineData("takenAt", "yesterday")]
    [InlineData("timeZone", "Mars/Olympus")]
    [InlineData("timeZone", "AUS Eastern Standard Time")]
    public async Task Invalid_field_returns_400_and_stores_nothing(string field, string value)
    {
        var fields = new Dictionary<string, string> { [field] = value };
        if (field == "price")
        {
            fields["currency"] = "VND";
        }
        if (field is "lat" or "lng")
        {
            fields[field == "lat" ? "lng" : "lat"] = "0";
        }

        var response = await PostAsync(fields);

        await AssertValidationErrorAsync(response, field);
        Assert.Equal(0, await CountEntriesAsync());
        Assert.False(StorageHasObjectsForUser());
    }

    [Fact]
    public async Task Empty_optional_fields_are_treated_as_missing()
    {
        // HTTP Shortcuts always sends every parameter, empty when the user skipped a step.
        var response = await PostAsync(new()
        {
            ["price"] = "",
            ["currency"] = "VND",
            ["lat"] = "",
            ["lng"] = "",
            ["placeId"] = "",
            ["placeName"] = "",
            ["category"] = "",
            ["takenAt"] = "",
            ["timeZone"] = "",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<EntryResponse>();
        var entry = await LoadEntryAsync(body!.Id);
        Assert.Null(entry!.Price);
        Assert.Null(entry.Currency);
        Assert.Null(entry.Lat);
        Assert.Null(entry.PlaceId);
        Assert.Null(entry.Category);
    }

    [Fact]
    public async Task TakenAt_from_javascript_toISOString_is_accepted()
    {
        var response = await PostAsync(new() { ["takenAt"] = "2026-11-01T05:30:00.123Z" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<EntryResponse>();
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 5, 30, 0, 123, TimeSpan.Zero), (await LoadEntryAsync(body!.Id))!.TakenAt);
    }

    [Fact]
    public async Task Category_is_stored_trimmed()
    {
        var response = await PostAsync(new() { ["category"] = "  Food " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<EntryResponse>();
        Assert.Equal("Food", (await LoadEntryAsync(body!.Id))!.Category);
    }

    [Fact]
    public async Task Too_long_category_returns_400()
    {
        await AssertValidationErrorAsync(await PostAsync(new() { ["category"] = new string('x', 51) }), "category");
    }

    [Fact]
    public async Task Price_without_currency_returns_400()
    {
        await AssertValidationErrorAsync(await PostAsync(new() { ["price"] = "10" }), "currency");
    }

    [Fact]
    public async Task Lat_without_lng_returns_400()
    {
        await AssertValidationErrorAsync(await PostAsync(new() { ["lat"] = "10" }), "lng");
    }

    [Fact]
    public async Task PlaceId_with_placeName_returns_400()
    {
        var response = await PostAsync(new() { ["placeId"] = Guid.NewGuid().ToString(), ["placeName"] = "Cafe" });

        await AssertValidationErrorAsync(response, "placeId");
    }

    [Fact]
    public async Task Missing_image_returns_400()
    {
        using var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", _user.ApiKey);
        using var form = new MultipartFormDataContent { { new StringContent("10"), "price" }, { new StringContent("AUD"), "currency" } };

        await AssertValidationErrorAsync(await client.PostAsync("/api/entries", form), "image");
    }

    [Fact]
    public async Task Non_image_file_returns_400()
    {
        var response = await PostAsync(image: "not an image"u8.ToArray());

        await AssertValidationErrorAsync(response, "image");
        Assert.False(StorageHasObjectsForUser());
    }

    [Fact]
    public async Task Same_place_name_reuses_the_place_ignoring_case()
    {
        var first = await (await PostAsync(new() { ["placeName"] = "Pho 24" })).Content.ReadFromJsonAsync<EntryResponse>();
        var second = await (await PostAsync(new() { ["placeName"] = "pho 24 " })).Content.ReadFromJsonAsync<EntryResponse>();

        Assert.NotNull(first!.PlaceId);
        Assert.Equal(first.PlaceId, second!.PlaceId);
    }

    [Fact]
    public async Task Same_place_name_from_another_user_creates_a_separate_place()
    {
        var other = await api.CreateUserAsync();

        var first = await (await PostAsync(new() { ["placeName"] = "Shared Name" })).Content.ReadFromJsonAsync<EntryResponse>();
        var second = await (await PostAsync(new() { ["placeName"] = "Shared Name" }, apiKey: other.ApiKey)).Content.ReadFromJsonAsync<EntryResponse>();

        Assert.NotEqual(first!.PlaceId, second!.PlaceId);
    }

    [Fact]
    public async Task PlaceId_of_own_or_google_place_is_accepted()
    {
        var other = await api.CreateUserAsync();
        var own = new Place { Name = "Mine", CreatedByUserId = _user.Id };
        var google = new Place { Name = "Google", GooglePlaceId = Guid.NewGuid().ToString(), CreatedByUserId = other.Id };
        await api.WithDbAsync(async db =>
        {
            db.Places.AddRange(own, google);
            await db.SaveChangesAsync();
        });

        foreach (var place in new[] { own, google })
        {
            var response = await PostAsync(new() { ["placeId"] = place.Id.ToString() });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<EntryResponse>();
            Assert.Equal(place.Id, body!.PlaceId);
        }
    }

    [Fact]
    public async Task PlaceId_of_another_users_manual_place_returns_400()
    {
        var other = await api.CreateUserAsync();
        var theirs = new Place { Name = "Theirs", CreatedByUserId = other.Id };
        await api.WithDbAsync(async db =>
        {
            db.Places.Add(theirs);
            await db.SaveChangesAsync();
        });

        await AssertValidationErrorAsync(await PostAsync(new() { ["placeId"] = theirs.Id.ToString() }), "placeId");
    }

    [Theory]
    [InlineData("full.webp")]
    [InlineData("thumb.webp")]
    public async Task Storage_failure_returns_502_and_leaves_nothing_behind(string failingFile)
    {
        api.Storage.FailUploadsEndingWith = failingFile;

        var response = await PostAsync(new() { ["placeName"] = "Should not be saved" });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal(0, await CountEntriesAsync());
        Assert.False(StorageHasObjectsForUser());

        var placeSaved = true;
        await api.WithDbAsync(async db => placeSaved = await db.Places.AnyAsync(p => p.CreatedByUserId == _user.Id));
        Assert.False(placeSaved);
    }

    private static async Task AssertValidationErrorAsync(HttpResponseMessage response, string field)
    {
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{(int)response.StatusCode}: {content}");

        using var json = JsonDocument.Parse(content);
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty(field, out _), content);
    }
}
