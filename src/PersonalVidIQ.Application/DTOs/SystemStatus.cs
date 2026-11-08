namespace PersonalVidIQ.Application.DTOs;

public sealed record SystemStatus(
    string Version,
    DateTimeOffset CheckedAtUtc,
    bool YouTubeApiKeyConfigured,
    DatabaseStatus Database,
    StorageStatus? Storage,
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
