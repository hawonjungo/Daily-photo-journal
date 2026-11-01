using Microsoft.EntityFrameworkCore;
using Relifes.Api.Data;

namespace Relifes.Api.Places;

public static class GoogleQuota
{
    /// <summary>
    /// Atomically takes one call from today's allowance. A single upsert means concurrent requests can never
    /// exceed the cap. The call is counted even if Google later fails, which errs on the side of spending less.
    /// </summary>
    public static async Task<bool> TryReserveAsync(AppDbContext db, int dailyCap, DateOnly today, CancellationToken cancellationToken)
    {
        if (dailyCap <= 0)
        {
            return false;
        }

        var counts = await db.Database.SqlQuery<int>($"""
            INSERT INTO "GoogleApiUsage" ("Date", "CallCount")
            VALUES ({today}, 1)
            ON CONFLICT ("Date") DO UPDATE
                SET "CallCount" = "GoogleApiUsage"."CallCount" + 1
                WHERE "GoogleApiUsage"."CallCount" < {dailyCap}
            RETURNING "CallCount"
            """).ToListAsync(cancellationToken);

        return counts.Count > 0;
    }
}
