namespace PersonalVidIQ.Application.DTOs;

public sealed record SystemStatus(
    string Version,
    DateTimeOffset CheckedAtUtc,
    bool YouTubeApiKeyConfigured,
    DatabaseStatus Database,
    SettingsSummary Settings,
    IReadOnlyList<string> Problems);

public sealed record DatabaseStatus(bool Reachable, string? ServerVersion, string? Error);

public sealed record SettingsSummary(
    int SnapshotRetentionDays,
    int ShortsMaxDurationSeconds,
    int DailySearchCallCap,
    int SearchCacheHours,
    int DailyQuotaUnits);
