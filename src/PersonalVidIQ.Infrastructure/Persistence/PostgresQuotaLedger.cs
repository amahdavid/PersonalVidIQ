using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PersonalVidIQ.Application.DTOs;
using PersonalVidIQ.Application.Interfaces;
using PersonalVidIQ.Application.Options;
using PersonalVidIQ.Infrastructure.YouTube;

namespace PersonalVidIQ.Infrastructure.Persistence;

/// <summary>Quota ledger shared by every PersonalVidIQ process (MCP server, CLI) through PostgreSQL.</summary>
public sealed class PostgresQuotaLedger(
    IDbContextFactory<PersonalVidIQDbContext> contextFactory,
    IOptions<YouTubeOptions> options,
    TimeProvider timeProvider) : IQuotaLedger
{
    public async Task RecordAsync(string endpoint, int units, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var dayStart = QuotaDay.StartUtc(now);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Atomic upsert so concurrent processes never lose a count.
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO quota_usage (quota_day_start_utc, endpoint, calls, units, last_call_at_utc)
            VALUES ({dayStart}, {endpoint}, 1, {units}, {now})
            ON CONFLICT (quota_day_start_utc, endpoint) DO UPDATE
            SET calls = quota_usage.calls + 1,
                units = quota_usage.units + EXCLUDED.units,
                last_call_at_utc = EXCLUDED.last_call_at_utc
            """,
            cancellationToken);
    }

    public async Task<QuotaUsage> GetUsageAsync(CancellationToken cancellationToken)
    {
        var dayStart = QuotaDay.StartUtc(timeProvider.GetUtcNow());
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.QuotaUsage.AsNoTracking().Where(q => q.QuotaDayStartUtc == dayStart).ToListAsync(cancellationToken);
        var yt = options.Value;

        return new QuotaUsage(
            dayStart,
            QuotaDay.NextStartUtc(dayStart),
            rows.Sum(r => r.Units),
            yt.DailyQuotaUnits,
            rows.Where(r => r.Endpoint == "search.list").Sum(r => r.Calls),
            yt.DailySearchCallCap,
            rows.ToDictionary(r => r.Endpoint, r => r.Calls, StringComparer.Ordinal));
    }
}
