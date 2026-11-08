using PersonalVidIQ.Application.Interfaces;

namespace PersonalVidIQ.Application.Collection;

public sealed record CollectRequest(string Trigger, bool Force = false, bool DryRun = false);

public sealed record NewUpload(string VideoId, string Title, string ChannelTitle, DateTimeOffset? PublishedAtUtc);

public sealed record CollectionReport(
    DateTimeOffset AsOfUtc,
    string Status,
    bool Forced,
    bool DryRun,
    int TrackedChannels,
    int TrackedVideos,
    int ChannelsSnapshotted,
    int UploadChecks,
    IReadOnlyList<NewUpload> NewUploadsTracked,
    IReadOnlyDictionary<string, int> VideosRefreshedByTier,
    int VideosNotDue,
    int VideosSkippedForQuota,
    IReadOnlyList<string> NoLongerAvailable,
    int SnapshotsSaved,
    int EstimatedUnits,
    int UnitsUsed,
    int UnitsRemainingToday,
    DateTimeOffset? NextDueUtc,
    RetentionResult? Purge,
    IReadOnlyList<string> Notes);
