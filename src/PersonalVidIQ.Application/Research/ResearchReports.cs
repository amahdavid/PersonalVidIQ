using PersonalVidIQ.Application.Analytics;
using PersonalVidIQ.Application.DTOs.Analysis;
using PersonalVidIQ.Application.YouTube;

namespace PersonalVidIQ.Application.Research;

public sealed record DataScope(
    string Source,
    int Channels,
    int Videos,
    int VideosWithoutStats,
    DateTimeOffset? OldestDataUtc,
    DateTimeOffset? NewestDataUtc);

public sealed record RelatedTerm(string Term, int Results);

public sealed record KeywordReport(
    string Keyword,
    bool SearchFromCache,
    long? YouTubeTotalResultsEstimate,
    int ResultsAnalysed,
    IReadOnlyList<CalculatedMetric> Competition,
    IReadOnlyList<RelatedTerm> RelatedTerms,
    IReadOnlyList<VideoListItem> TopResults,
    QuotaSnapshot Quota,
    IReadOnlyList<string> Notes);

public sealed record KeywordIdeasReport(
    string? Seed,
    VideoFormat? Format,
    DataScope Data,
    IReadOnlyList<TermStat> BestPerformingKeywords,
    IReadOnlyList<TermStat> MostUsedKeywords,
    IReadOnlyList<TopicGroup> Topics,
    string Formula,
    IReadOnlyList<string> Notes);

public sealed record TrendReport(
    int WindowDays,
    VideoFormat? Format,
    DataScope Data,
    IReadOnlyList<EmergingTerm> Emerging,
    string Formula,
    IReadOnlyList<string> Notes);

public sealed record RecentUpload(VideoEvidence Video, CalculatedMetric ViewsPerDayLast24h, double? AgeHours);

public sealed record RecentUploadsReport(int Hours, DataScope Data, IReadOnlyList<RecentUpload> Uploads, IReadOnlyList<string> Notes);

public sealed record KeywordGap(string Term, int CompetitorVideos, int CompetitorChannels, double? MedianChannelRelativeRatio, IReadOnlyList<VideoEvidence> Examples);

public sealed record IdeaEvidenceReport(
    string? MyChannel,
    int WindowDays,
    DataScope Data,
    IReadOnlyList<VideoEvidence> RecentOutliers,
    IReadOnlyList<TermStat> BestPerformingKeywords,
    IReadOnlyList<EmergingTerm> EmergingKeywords,
    IReadOnlyList<KeywordGap> KeywordGaps,
    IReadOnlyList<string> HowToUse,
    IReadOnlyList<string> Notes);
