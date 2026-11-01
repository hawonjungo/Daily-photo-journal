using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Relifes.Api.Places;

namespace Relifes.Api.Tests;

public class GooglePlacesClientTests
{
    private class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private static GooglePlacesClient Client(StubHandler handler) =>
        new(new HttpClient(handler), Options.Create(new PlacesOptions { GoogleApiKey = "secret-key" }));

    [Fact]
    public async Task Request_uses_only_the_allowed_field_mask_and_search_parameters()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{}");

        await Client(handler).SearchNearbyAsync(10.5, 106.5, 80, 10, CancellationToken.None);

        var request = handler.Request!;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://places.googleapis.com/v1/places:searchNearby", request.RequestUri!.ToString());
        Assert.Equal("secret-key", request.Headers.GetValues("X-Goog-Api-Key").Single());
        Assert.Equal(
            "places.id,places.displayName,places.formattedAddress,places.location",
            request.Headers.GetValues("X-Goog-FieldMask").Single());

        using var body = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal(10, body.RootElement.GetProperty("maxResultCount").GetInt32());
        var circle = body.RootElement.GetProperty("locationRestriction").GetProperty("circle");
        Assert.Equal(80, circle.GetProperty("radius").GetDouble());
        Assert.Equal(10.5, circle.GetProperty("center").GetProperty("latitude").GetDouble());
        Assert.Equal(106.5, circle.GetProperty("center").GetProperty("longitude").GetDouble());
    }

    [Fact]
    public async Task Response_is_mapped_and_incomplete_places_are_skipped()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """
            {
              "places": [
                {
                  "id": "abc",
                  "displayName": { "text": "Bun Bo", "languageCode": "vi" },
                  "formattedAddress": "1 Le Loi",
                  "location": { "latitude": 10.77, "longitude": 106.7 }
                },
                { "id": "no-name", "location": { "latitude": 1, "longitude": 2 } },
                { "id": "no-location", "displayName": { "text": "X" } }
              ]
            }
            """);

        var result = await Client(handler).SearchNearbyAsync(10, 106, 80, 10, CancellationToken.None);

        var place = Assert.Single(result);
        Assert.Equal(new ExternalPlace("abc", "Bun Bo", "1 Le Loi", 10.77, 106.7), place);
    }

    [Fact]
    public async Task Empty_response_returns_no_places()
    {
        var result = await Client(new StubHandler(HttpStatusCode.OK, "{}")).SearchNearbyAsync(10, 106, 80, 10, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Error_status_throws()
    {
        var client = Client(new StubHandler(HttpStatusCode.Forbidden, "{}"));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.SearchNearbyAsync(10, 106, 80, 10, CancellationToken.None));
    }
}
