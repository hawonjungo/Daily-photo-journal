namespace Relifes.Api.Places;

public record ExternalPlace(string GooglePlaceId, string Name, string? Address, double Lat, double Lng);

public interface IPlacesClient
{
    Task<IReadOnlyList<ExternalPlace>> SearchNearbyAsync(
        double lat,
        double lng,
        double radiusMeters,
        int maxResults,
        CancellationToken cancellationToken);
}
