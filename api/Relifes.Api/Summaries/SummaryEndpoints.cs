using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Relifes.Api.Data;
using Relifes.Api.Entries;

namespace Relifes.Api.Summaries;

public static class SummaryEndpoints
{
    public static void MapSummaryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/summary", GetSummary).RequireAuthorization();
    }

    private static async Task<IResult> GetSummary(
        string? period,
        string? date,
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (!SummaryPeriod.IsValid(period))
        {
            errors["period"] = ["Must be day, week or month."];
        }

        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            errors["date"] = ["Use the format yyyy-MM-dd."];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (from, to) = SummaryCalculator.RangeFor(period!, day);
        var (start, end) = LocalDates.UtcWindow(from, to);
        var entries = await db.Entries
            .AsNoTracking()
            .Include(e => e.Place)
            .Where(e => e.UserId == userId && e.TakenAt >= start && e.TakenAt < end)
            .ToListAsync(cancellationToken);

        return Results.Ok(SummaryCalculator.Calculate(period!, day, entries));
    }
}
