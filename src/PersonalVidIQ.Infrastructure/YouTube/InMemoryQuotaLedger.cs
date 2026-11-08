using Microsoft.Extensions.Options;
using PersonalVidIQ.Application.DTOs;
using PersonalVidIQ.Application.Interfaces;
using PersonalVidIQ.Application.Options;

namespace PersonalVidIQ.Infrastructure.YouTube;

/// <summary>
/// Per-process quota counter. Resets when the YouTube quota day rolls over (midnight Pacific Time).
/// Milestone 2 replaces this with a PostgreSQL-backed ledger so the MCP server and the CLI share one count.
/// </summary>
public sealed class InMemoryQuotaLedger(IOptions<YouTubeOptions> options, TimeProvider timeProvider) : IQuotaLedger
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, int> _calls = new(StringComparer.Ordinal);
    private DateTimeOffset _dayStartUtc = DateTimeOffset.MinValue;
    private int _units;

    public void Record(string endpoint, int units)
    {
        lock (_gate)
        {
            RollOverIfNeeded();
            _units += units;
            _calls[endpoint] = _calls.GetValueOrDefault(endpoint) + 1;
        }
    }

    public QuotaUsage GetUsage()
    {
        lock (_gate)
        {
            RollOverIfNeeded();
            var yt = options.Value;
            return new QuotaUsage(
                _dayStartUtc,
                QuotaDay.NextStartUtc(_dayStartUtc),
                _units,
                yt.DailyQuotaUnits,
                _calls.GetValueOrDefault("search.list"),
                yt.DailySearchCallCap,
                new Dictionary<string, int>(_calls, StringComparer.Ordinal));
        }
    }

    private void RollOverIfNeeded()
    {
        var currentDayStart = QuotaDay.StartUtc(timeProvider.GetUtcNow());
        if (currentDayStart != _dayStartUtc)
        {
            _dayStartUtc = currentDayStart;
            _units = 0;
            _calls.Clear();
        }
    }
}

/// <summary>YouTube's quota day runs midnight to midnight Pacific Time.</summary>
public static class QuotaDay
{
    private static readonly TimeZoneInfo Pacific = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");

    public static DateTimeOffset StartUtc(DateTimeOffset nowUtc)
    {
        var local = TimeZoneInfo.ConvertTime(nowUtc, Pacific);
        var midnight = new DateTime(local.Year, local.Month, local.Day, 0, 0, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(midnight, Pacific.GetUtcOffset(midnight)).ToUniversalTime();
    }

    public static DateTimeOffset NextStartUtc(DateTimeOffset dayStartUtc) =>
        // 26 h always lands inside the next Pacific day, even across a DST change.
        StartUtc(dayStartUtc.AddHours(26));
}
