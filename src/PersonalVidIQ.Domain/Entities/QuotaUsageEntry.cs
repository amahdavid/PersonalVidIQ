namespace PersonalVidIQ.Domain.Entities;

/// <summary>Quota spent on one endpoint in one quota day. Shared by every PersonalVidIQ process.</summary>
public sealed class QuotaUsageEntry
{
    public DateTimeOffset QuotaDayStartUtc { get; set; }

    public required string Endpoint { get; set; }

    public int Calls { get; set; }

    public int Units { get; set; }

    public DateTimeOffset LastCallAtUtc { get; set; }
}
