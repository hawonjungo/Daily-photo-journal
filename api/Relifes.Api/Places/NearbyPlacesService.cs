using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Npgsql;
using Relifes.Api.Data;
using Relifes.Api.Entities;

namespace Relifes.Api.Places;

public record NearbyPlace(Guid Id, string Name, string? Address, double Lat, double Lng, int DistanceMeters);

public record NearbyPlacesResponse(IReadOnlyList<NearbyPlace> Places, string Source, string? FallbackReason);

public static class NearbySource
{
    public const string Database = "database";
    public const string Google = "google";
    public const string None = "none";
}

public static class NearbyFallback
{
    public const string NoResults = "no_results";
    public const string DailyLimitReached = "daily_limit_reached";
    public const string GoogleError = "google_error";
    public const string NotConfigured = "not_configured";
}

public class NearbyPlacesService(
    AppDbContext db,
    IPlacesClient google,
    IMemoryCache cache,
    IOptions<PlacesOptions> options,
    ILogger<NearbyPlacesService> logger)
{
    public const int MaxResults = 10;

    private static readonly TimeSpan EmptyResultLifetime = TimeSpan.FromHours(24);

    private readonly PlacesOptions _options = options.Value;

    public async Task<NearbyPlacesResponse> FindAsync(Guid userId, double lat, double lng, CancellationToken cancellationToken)
    {
        var known = await FindKnownAsync(userId, lat, lng, cancellationToken);
        if (known.Count > 0)
        {
            return new NearbyPlacesResponse(known, NearbySource.Database, null);
        }

        // Rounded to about 11 m, so GPS jitter at an empty spot does not spend a new Google call each time.
        var emptyKey = $"nearby-empty:{Math.Round(lat, 4)}:{Math.Round(lng, 4)}";
        if (cache.TryGetValue(emptyKey, out _))
        {
            return Fallback(NearbyFallback.NoResults);
        }

        if (string.IsNullOrWhiteSpace(_options.GoogleApiKey))
        {
            return Fallback(NearbyFallback.NotConfigured);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (!await GoogleQuota.TryReserveAsync(db, _options.DailyGoogleCap, today, cancellationToken))
        {
            logger.LogWarning("Daily Google Places cap of {Cap} reached", _options.DailyGoogleCap);
            return Fallback(NearbyFallback.DailyLimitReached);
        }

        IReadOnlyList<ExternalPlace> results;
        try
        {
            results = await google.SearchNearbyAsync(lat, lng, _options.SearchRadiusMeters, MaxResults, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested
            && ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogError(ex, "Google Places request failed");
            return Fallback(NearbyFallback.GoogleError);
        }

        if (results.Count == 0)
        {
            cache.Set(emptyKey, true, EmptyResultLifetime);
            return Fallback(NearbyFallback.NoResults);
        }

        var saved = await SaveAsync(userId, results, cancellationToken);
        var places = saved
            .Select(p => ToNearby(p, lat, lng))
            .OrderBy(p => p.DistanceMeters)
            .Take(MaxResults)
            .ToList();

        return new NearbyPlacesResponse(places, NearbySource.Google, null);
    }

    private static NearbyPlacesResponse Fallback(string reason) => new([], NearbySource.None, reason);

    private async Task<List<NearbyPlace>> FindKnownAsync(Guid userId, double lat, double lng, CancellationToken cancellationToken)
    {
        var radius = _options.SearchRadiusMeters;
        var (latDelta, lngDelta) = GeoDistance.BoundingBox(lat, radius);

        // Places from Google are shared between users; manually named ones belong to their creator.
        var candidates = await db.Places
            .AsNoTracking()
            .Where(p => p.Lat >= lat - latDelta && p.Lat <= lat + latDelta
                && p.Lng >= lng - lngDelta && p.Lng <= lng + lngDelta
                && (p.GooglePlaceId != null || p.CreatedByUserId == userId))
            .ToListAsync(cancellationToken);

        return candidates
            .Where(p => GeoDistance.Meters(lat, lng, p.Lat!.Value, p.Lng!.Value) <= radius)
            .Select(p => ToNearby(p, lat, lng))
            .OrderBy(p => p.DistanceMeters)
            .Take(MaxResults)
            .ToList();
    }

    private async Task<List<Place>> SaveAsync(Guid userId, IReadOnlyList<ExternalPlace> results, CancellationToken cancellationToken)
    {
        var ids = results.Select(r => r.GooglePlaceId).ToList();
        var existing = await db.Places
            .Where(p => p.GooglePlaceId != null && ids.Contains(p.GooglePlaceId))
            .ToDictionaryAsync(p => p.GooglePlaceId!, cancellationToken);

        foreach (var result in results.DistinctBy(r => r.GooglePlaceId))
        {
            if (!existing.TryGetValue(result.GooglePlaceId, out var place))
            {
                place = new Place { GooglePlaceId = result.GooglePlaceId, Name = result.Name, CreatedByUserId = userId };
                db.Places.Add(place);
                existing[result.GooglePlaceId] = place;
            }

            place.Name = Truncate(result.Name, 200);
            place.Address = result.Address is null ? null : Truncate(result.Address, 500);
            place.Lat = result.Lat;
            place.Lng = result.Lng;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return existing.Values.ToList();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Another request saved the same place first, so its rows are the ones to use.
            db.ChangeTracker.Clear();
            return await db.Places
                .AsNoTracking()
                .Where(p => p.GooglePlaceId != null && ids.Contains(p.GooglePlaceId))
                .ToListAsync(cancellationToken);
        }
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static NearbyPlace ToNearby(Place place, double lat, double lng) => new(
        place.Id,
        place.Name,
        place.Address,
        place.Lat!.Value,
        place.Lng!.Value,
        (int)Math.Round(GeoDistance.Meters(lat, lng, place.Lat.Value, place.Lng.Value)));
}
