namespace PersonalVidIQ.Application.DTOs;

/// <summary>
/// Quota spent in the current YouTube quota day (resets at midnight Pacific Time).
/// Counted locally from the calls this app made; YouTube's own console is authoritative.
/// </summary>
public sealed record QuotaUsage(
    DateTimeOffset QuotaDayStartUtc,
    DateTimeOffset QuotaDayResetsAtUtc,
    int UnitsUsed,
    int DailyUnitLimit,
    int SearchCallsUsed,
    int SearchCallCap,
    IReadOnlyDictionary<string, int> CallsByEndpoint)
{
    public int UnitsRemaining => Math.Max(0, DailyUnitLimit - UnitsUsed);

    public int SearchCallsRemaining => Math.Max(0, SearchCallCap - SearchCallsUsed);
}
