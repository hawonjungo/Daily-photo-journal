using System.Globalization;
using System.Text.RegularExpressions;

namespace Relifes.Api.Entries;

/// <summary>
/// Multipart fields of POST /api/entries. Values are strings and parsed here because the default binder
/// reads "12,5" as 125 and "50.000" as 50, which would silently record the wrong price.
/// </summary>
public class CreateEntryForm
{
    public IFormFile? Image { get; set; }
    public string? Price { get; set; }
    public string? Currency { get; set; }
    public string? Lat { get; set; }
    public string? Lng { get; set; }
    public string? PlaceId { get; set; }
    public string? PlaceName { get; set; }
    public string? TakenAt { get; set; }
    public string? TimeZone { get; set; }
    public string? Category { get; set; }

    public ParsedEntryForm Parse(Dictionary<string, string[]> errors)
    {
        var price = ParsePrice(errors);
        var currency = ParseCurrency(price, errors);
        var (lat, lng) = ParseLocation(errors);
        var (placeId, placeName) = ParsePlace(errors);
        var takenAt = ParseTakenAt(errors);
        var timeZone = ParseTimeZone(errors);
        var category = ParseCategory(errors);

        return new ParsedEntryForm(price, currency, lat, lng, placeId, placeName, takenAt, timeZone, category);
    }

    private string? ParseCategory(Dictionary<string, string[]> errors)
    {
        var value = Category?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (value.Length > 50)
        {
            errors["category"] = ["Must be at most 50 characters."];
            return null;
        }

        return value;
    }

    private decimal? ParsePrice(Dictionary<string, string[]> errors)
    {
        var value = Price?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (!Regex.IsMatch(value, @"^[0-9]{1,16}(\.[0-9]{1,2})?$"))
        {
            errors["price"] = ["Use digits with an optional '.' and at most 2 decimals, e.g. 12.50 or 50000."];
            return null;
        }

        return decimal.Parse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
    }

    private string? ParseCurrency(decimal? price, Dictionary<string, string[]> errors)
    {
        var value = Currency?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(value))
        {
            if (price is not null)
            {
                errors["currency"] = ["Currency is required when price is set."];
            }
            return null;
        }

        if (!Regex.IsMatch(value, "^[A-Z]{3}$"))
        {
            errors["currency"] = ["Use a 3-letter ISO code, e.g. AUD or VND."];
            return null;
        }

        return price is null ? null : value;
    }

    private (double? Lat, double? Lng) ParseLocation(Dictionary<string, string[]> errors)
    {
        var lat = ParseCoordinate("lat", Lat, 90, errors);
        var lng = ParseCoordinate("lng", Lng, 180, errors);

        if (errors.ContainsKey("lat") || errors.ContainsKey("lng"))
        {
            return (null, null);
        }

        if (lat is null != lng is null)
        {
            errors[lat is null ? "lat" : "lng"] = ["lat and lng must be sent together."];
            return (null, null);
        }

        return (lat, lng);
    }

    private static double? ParseCoordinate(string field, string? value, double limit, Dictionary<string, string[]> errors)
    {
        value = value?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var coordinate)
            || !double.IsFinite(coordinate)
            || Math.Abs(coordinate) > limit)
        {
            errors[field] = [$"Must be a number between -{limit} and {limit}."];
            return null;
        }

        return coordinate;
    }

    private (Guid? PlaceId, string? PlaceName) ParsePlace(Dictionary<string, string[]> errors)
    {
        var idValue = PlaceId?.Trim();
        var name = PlaceName?.Trim();
        var hasId = !string.IsNullOrEmpty(idValue);
        var hasName = !string.IsNullOrEmpty(name);

        if (hasId && hasName)
        {
            errors["placeId"] = ["Send either placeId or placeName, not both."];
            return (null, null);
        }

        if (hasId)
        {
            if (Guid.TryParse(idValue, out var id))
            {
                return (id, null);
            }

            errors["placeId"] = ["Must be a valid id."];
            return (null, null);
        }

        if (hasName && name!.Length > 200)
        {
            errors["placeName"] = ["Must be at most 200 characters."];
            return (null, null);
        }

        return (null, hasName ? name : null);
    }

    private DateTimeOffset? ParseTakenAt(Dictionary<string, string[]> errors)
    {
        var value = TakenAt?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        // A time without an offset would be read in the server's time zone, so it is rejected.
        if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            || parsed.Kind == DateTimeKind.Unspecified
            || !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var takenAt))
        {
            errors["takenAt"] = ["Use ISO 8601 with an offset, e.g. 2026-11-01T12:30:00+11:00 or 2026-11-01T01:30:00Z."];
            return null;
        }

        return takenAt.ToUniversalTime();
    }

    private string? ParseTimeZone(Dictionary<string, string[]> errors)
    {
        var value = TimeZone?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(value, out var zone) || !zone.HasIanaId)
        {
            errors["timeZone"] = ["Use an IANA time zone name, e.g. Australia/Sydney."];
            return null;
        }

        return value;
    }
}

public record ParsedEntryForm(
    decimal? Price,
    string? Currency,
    double? Lat,
    double? Lng,
    Guid? PlaceId,
    string? PlaceName,
    DateTimeOffset? TakenAt,
    string? TimeZone,
    string? Category);
