namespace PersonalVidIQ.Application.DTOs;

public sealed record SystemStatus(
    string Version,
    DateTimeOffset CheckedAtUtc,
    bool YouTubeApiKeyConfigured,
    DatabaseStatus Database,
    StorageStatus? Storage,
    CollectionStatus? Collection,
    SettingsSummary Settings,
    QuotaUsage Quota,
    IReadOnlyList<string> Problems);

public sealed record DatabaseStatus(bool Reachable, string? ServerVersion, string? Error);

public sealed record SettingsSummary(
    int SnapshotRetentionDays,
    int ShortsMaxDurationSeconds,
    int DailySearchCallCap,
    int SearchCacheHours,
    int DailyQuotaUnits);

/// <summary>What is stored locally. Null when the database is unreachable.</summary>
public sealed record StorageStatus(
    IReadOnlyList<string> PendingMigrations,
    int TrackedChannels,
    int TrackedVideos,
    int StoredVideos,
    int VideoSnapshots,
    int ChannelSnapshots,
    DateTimeOffset? OldestSnapshotUtc);

/// <summary>Refresh state: what's due now and how the last refresh went.</summary>
public sealed record CollectionStatus(
    int ChannelsDue,
    int UploadChecksDue,
    int VideosDue,
    DateTimeOffset? NextDueUtc,
    DateTimeOffset? LastRunAtUtc,
    string? LastRunStatus,
    int? LastRunUnitsUsed,
    string? LastRunError);
