namespace Relifes.Api.Places;

public class PlacesOptions
{
    public const string SectionName = "Places";

    /// <summary>Without a key the API never calls Google and clients fall back to a manual place name.</summary>
    public string? GoogleApiKey { get; set; }

    public int DailyGoogleCap { get; set; } = 150;
    public double SearchRadiusMeters { get; set; } = 80;
}
