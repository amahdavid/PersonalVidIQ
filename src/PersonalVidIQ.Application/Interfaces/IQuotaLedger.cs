using PersonalVidIQ.Application.DTOs;

namespace PersonalVidIQ.Application.Interfaces;

/// <summary>Counts YouTube API quota spent in the current quota day (midnight to midnight Pacific Time).</summary>
public interface IQuotaLedger
{
    /// <summary>Records one API request. Called for every attempt, including failed ones, because YouTube charges for those too.</summary>
    Task RecordAsync(string endpoint, int units, CancellationToken cancellationToken);

    Task<QuotaUsage> GetUsageAsync(CancellationToken cancellationToken);
}
