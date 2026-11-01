using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Relifes.Api.Places;

public class GooglePlacesClient(HttpClient http, IOptions<PlacesOptions> options) : IPlacesClient
{
    public const string SearchNearbyUrl = "https://places.googleapis.com/v1/places:searchNearby";

    // Stays in the Pro SKU. Never add rating, hours or photos: they move every call to a pricier SKU.
    public const string FieldMask = "places.id,places.displayName,places.formattedAddress,places.location";

    public async Task<IReadOnlyList<ExternalPlace>> SearchNearbyAsync(
        double lat,
        double lng,
        double radiusMeters,
        int maxResults,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, SearchNearbyUrl)
        {
            Content = JsonContent.Create(new
            {
                maxResultCount = maxResults,
                rankPreference = "DISTANCE",
                locationRestriction = new
                {
                    circle = new
                    {
                        center = new { latitude = lat, longitude = lng },
                        radius = radiusMeters,
                    },
                },
            }),
        };
        request.Headers.Add("X-Goog-Api-Key", options.Value.GoogleApiKey);
        request.Headers.Add("X-Goog-FieldMask", FieldMask);

        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<SearchNearbyResponse>(cancellationToken);

        return (body?.Places ?? [])
            .Where(p => !string.IsNullOrEmpty(p.Id) && !string.IsNullOrEmpty(p.DisplayName?.Text) && p.Location is not null)
            .Select(p => new ExternalPlace(p.Id!, p.DisplayName!.Text!, p.FormattedAddress, p.Location!.Latitude, p.Location.Longitude))
            .ToList();
    }

    private record SearchNearbyResponse([property: JsonPropertyName("places")] List<GooglePlace>? Places);

    private record GooglePlace(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("displayName")] GoogleText? DisplayName,
        [property: JsonPropertyName("formattedAddress")] string? FormattedAddress,
        [property: JsonPropertyName("location")] GoogleLocation? Location);

    private record GoogleText([property: JsonPropertyName("text")] string? Text);

    private record GoogleLocation(
        [property: JsonPropertyName("latitude")] double Latitude,
        [property: JsonPropertyName("longitude")] double Longitude);
}
