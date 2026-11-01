using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Relifes.Api.Data;
using Relifes.Api.Entities;
using Relifes.Api.Images;
using Relifes.Api.Storage;

namespace Relifes.Api.Entries;

public static class EntryEndpoints
{
    private const long MaxImageBytes = 20 * 1024 * 1024;
    private const long MaxRequestBytes = MaxImageBytes + 1024 * 1024;
    private const int DefaultTimelineDays = 30;
    private const int MaxTimelineDays = 366;

    private static readonly TimeSpan ImageUrlLifetime = TimeSpan.FromHours(6);

    public static void MapEntryEndpoints(this IEndpointRouteBuilder app)
    {
        var entries = app.MapGroup("/api/entries").RequireAuthorization();

        entries.MapGet("/", GetTimeline);

        entries.MapPost("/", CreateEntry)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaxRequestBytes));
    }

    private static async Task<IResult> GetTimeline(
        string? from,
        string? to,
        ClaimsPrincipal principal,
        AppDbContext db,
        IImageStorage storage,
        CancellationToken cancellationToken)
    {
        // Default window ends tomorrow in UTC, because it is already tomorrow in time zones ahead of UTC.
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var errors = new Dictionary<string, string[]>();
        var toDate = ParseDate("to", to, tomorrow, errors);
        var fromDate = ParseDate("from", from, toDate.AddDays(-DefaultTimelineDays), errors);

        if (errors.Count == 0 && fromDate > toDate)
        {
            errors["from"] = ["Must not be after to."];
        }
        else if (errors.Count == 0 && toDate.DayNumber - fromDate.DayNumber > MaxTimelineDays)
        {
            errors["from"] = [$"The range can cover at most {MaxTimelineDays} days."];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (start, end) = LocalDates.UtcWindow(fromDate, toDate);
        var candidates = await db.Entries
            .AsNoTracking()
            .Include(e => e.Place)
            .Where(e => e.UserId == userId && e.TakenAt >= start && e.TakenAt < end)
            .OrderByDescending(e => e.TakenAt)
            .ToListAsync(cancellationToken);

        var days = new List<TimelineDay>();
        foreach (var group in candidates
            .GroupBy(e => LocalDates.Of(e.TakenAt, e.TimeZone))
            .Where(g => g.Key >= fromDate && g.Key <= toDate)
            .OrderByDescending(g => g.Key))
        {
            var entries = new List<TimelineEntry>();
            foreach (var entry in group)
            {
                entries.Add(new TimelineEntry(
                    entry.Id,
                    entry.TakenAt,
                    entry.TimeZone,
                    await storage.GetDownloadUrlAsync(entry.ImageKey, ImageUrlLifetime),
                    await storage.GetDownloadUrlAsync(entry.ThumbKey, ImageUrlLifetime),
                    entry.Price,
                    entry.Currency,
                    entry.PlaceId,
                    entry.Place?.Name,
                    entry.Category,
                    entry.Note,
                    entry.Rating));
            }

            days.Add(new TimelineDay(group.Key, entries.Count, CurrencyTotal.For(group), entries));
        }

        return Results.Ok(new TimelineResponse(fromDate, toDate, days));
    }

    private static DateOnly ParseDate(string field, string? value, DateOnly fallback, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        errors[field] = ["Use the format yyyy-MM-dd."];
        return fallback;
    }

    private static async Task<IResult> CreateEntry(
        [FromForm] CreateEntryForm form,
        ClaimsPrincipal principal,
        AppDbContext db,
        IImageStorage storage,
        IConfiguration configuration,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var input = form.Parse(errors);

        if (form.Image is null or { Length: 0 })
        {
            errors["image"] = ["An image is required."];
        }
        else if (form.Image.Length > MaxImageBytes)
        {
            return Results.Problem("Image is larger than 20 MB.", statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

        Place? place = null;
        if (input.PlaceId is { } placeId)
        {
            // Places from Google are shared between users; manually named ones belong to their creator.
            place = await db.Places.SingleOrDefaultAsync(
                p => p.Id == placeId && (p.GooglePlaceId != null || p.CreatedByUserId == userId),
                cancellationToken);

            if (place is null)
            {
                errors["placeId"] = ["Place not found."];
            }
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        ProcessedImage image;
        try
        {
            await using var upload = form.Image!.OpenReadStream();
            image = ImageProcessor.Process(upload);
        }
        catch (InvalidImageException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["image"] = [ex.Message] });
        }

        if (input.PlaceName is { } placeName)
        {
            place = await FindOrCreateManualPlace(db, userId, placeName, input.Lat, input.Lng, cancellationToken);
        }

        var now = DateTimeOffset.UtcNow;
        var entry = new Entry
        {
            UserId = userId,
            TakenAt = input.TakenAt ?? now,
            TimeZone = input.TimeZone ?? configuration["Entries:DefaultTimeZone"] ?? "UTC",
            ImageKey = "",
            ThumbKey = "",
            Price = input.Price,
            Currency = input.Currency,
            Place = place,
            Lat = input.Lat,
            Lng = input.Lng,
            Category = input.Category,
            CreatedAt = now,
            UpdatedAt = now,
        };
        entry.ImageKey = $"users/{userId}/entries/{entry.Id}/full.webp";
        entry.ThumbKey = $"users/{userId}/entries/{entry.Id}/thumb.webp";
        db.Entries.Add(entry);

        var logger = loggerFactory.CreateLogger(typeof(EntryEndpoints));
        try
        {
            await storage.UploadAsync(entry.ImageKey, image.Full, ImageProcessor.ContentType, cancellationToken);
            await storage.UploadAsync(entry.ThumbKey, image.Thumbnail, ImageProcessor.ContentType, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Uploading images for entry {EntryId} failed", entry.Id);
            await DeleteImages(storage, entry, logger);
            return Results.Problem("Could not store the image.", statusCode: StatusCodes.Status502BadGateway);
        }
        catch
        {
            await DeleteImages(storage, entry, logger);
            throw;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await DeleteImages(storage, entry, logger);
            throw;
        }

        return Results.Created($"/api/entries/{entry.Id}", EntryResponse.From(entry));
    }

    private static async Task<Place> FindOrCreateManualPlace(
        AppDbContext db,
        Guid userId,
        string name,
        double? lat,
        double? lng,
        CancellationToken cancellationToken)
    {
        var existing = await db.Places.FirstOrDefaultAsync(
            p => p.CreatedByUserId == userId && p.GooglePlaceId == null && p.Name.ToLower() == name.ToLower(),
            cancellationToken);

        return existing ?? new Place { Name = name, Lat = lat, Lng = lng, CreatedByUserId = userId };
    }

    // Best effort, and not tied to the request: the client may already have gone away.
    private static async Task DeleteImages(IImageStorage storage, Entry entry, ILogger logger)
    {
        foreach (var key in new[] { entry.ImageKey, entry.ThumbKey })
        {
            try
            {
                await storage.DeleteAsync(key, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not delete orphaned image {Key}", key);
            }
        }
    }
}
