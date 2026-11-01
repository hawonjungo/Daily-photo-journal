using System.Globalization;
using System.Security.Claims;

namespace Relifes.Api.Places;

public static class PlaceEndpoints
{
    public static void MapPlaceEndpoints(this IEndpointRouteBuilder app)
    {
        var places = app.MapGroup("/api/places").RequireAuthorization();

        places.MapGet("/nearby", GetNearby);
    }

    private static async Task<IResult> GetNearby(
        string? lat,
        string? lng,
        ClaimsPrincipal principal,
        NearbyPlacesService service,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var latitude = ParseCoordinate("lat", lat, 90, errors);
        var longitude = ParseCoordinate("lng", lng, 180, errors);

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Results.Ok(await service.FindAsync(userId, latitude, longitude, cancellationToken));
    }

    private static double ParseCoordinate(string field, string? value, double limit, Dictionary<string, string[]> errors)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var coordinate)
            && double.IsFinite(coordinate)
            && Math.Abs(coordinate) <= limit)
        {
            return coordinate;
        }

        errors[field] = [$"Must be a number between -{limit} and {limit}."];
        return 0;
    }
}
