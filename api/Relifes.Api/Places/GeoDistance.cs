namespace Relifes.Api.Places;

public static class GeoDistance
{
    private const double EarthRadiusMeters = 6_371_000;
    private const double MetersPerDegreeLatitude = 111_320;

    public static double Meters(double lat1, double lng1, double lat2, double lng2)
    {
        var dLat = double.DegreesToRadians(lat2 - lat1);
        var dLng = double.DegreesToRadians(lng2 - lng1);
        var a = Math.Pow(Math.Sin(dLat / 2), 2)
            + Math.Cos(double.DegreesToRadians(lat1)) * Math.Cos(double.DegreesToRadians(lat2)) * Math.Pow(Math.Sin(dLng / 2), 2);

        return 2 * EarthRadiusMeters * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    /// <summary>Half-size in degrees of a box containing the circle, so the database can use its index.</summary>
    public static (double LatDelta, double LngDelta) BoundingBox(double lat, double radiusMeters)
    {
        var latDelta = radiusMeters / MetersPerDegreeLatitude;
        var lngDelta = latDelta / Math.Max(Math.Cos(double.DegreesToRadians(lat)), 0.01);
        return (latDelta, lngDelta);
    }
}
