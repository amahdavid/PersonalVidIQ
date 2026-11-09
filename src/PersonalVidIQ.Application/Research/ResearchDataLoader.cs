using Microsoft.Extensions.Options;
using PersonalVidIQ.Application.Analytics;
using PersonalVidIQ.Application.Interfaces;
using PersonalVidIQ.Application.Options;
using PersonalVidIQ.Application.YouTube;

namespace PersonalVidIQ.Application.Research;

public sealed record ResearchDataset(
    IReadOnlyList<ResearchVideo> Videos,
    int Channels,
    DateTimeOffset? OldestDataUtc,
    DateTimeOffset? NewestDataUtc,
    int VideosWithoutSnapshots);

/// <summary>
/// Builds research input from stored data only (no quota): each video's latest stored view count, its history,
/// and its channel-relative ratio from <see cref="OutlierScorer"/> against its own channel's stored videos.
/// </summary>
public sealed class ResearchDataLoader(IResearchDataStore store, IOptions<DataCollectionOptions> options, TimeProvider timeProvider)
{
    public async Task<ResearchDataset> LoadAsync(IReadOnlyCollection<string>? channelIds, CancellationToken cancellationToken)
    {
        var o = options.Value;
        var now = timeProvider.GetUtcNow();
        var records = await store.GetChannelVideosAsync(channelIds, now.AddDays(-o.SnapshotRetentionDays), cancellationToken);
        var settings = new OutlierSettings(ShortsMaxDurationSeconds: o.ShortsMaxDurationSeconds);
        var videos = new List<ResearchVideo>(records.Count);

        foreach (var channel in records.GroupBy(r => r.ChannelId))
        {
            var inputs = channel.Select(ToInput).ToList();
            var scores = OutlierScorer.ScoreAll(inputs, settings, now).ToDictionary(s => s.VideoId, StringComparer.Ordinal);
            foreach (var (record, input) in channel.Zip(inputs))
            {
                var duration = record.DurationSeconds is { } d ? TimeSpan.FromSeconds(d) : (TimeSpan?)null;
                double? perDay = input.Views is { } views && input.PublishedAtUtc is { } published && record.Snapshots.Count > 0
                    ? views / LifetimeAverage.AgeDays(published, input.CapturedAtUtc)
                    : null;
                videos.Add(new ResearchVideo(
                    input,
                    record.ChannelId,
                    record.ChannelTitle,
                    record.Tags,
                    VideoFormatClassifier.Classify(duration, o.ShortsMaxDurationSeconds),
                    scores[record.VideoId].Ratio,
                    perDay));
            }
        }

        var captured = records.Where(r => r.Snapshots.Count > 0).Select(r => r.Snapshots[^1].AtUtc).ToList();
        return new ResearchDataset(
            videos,
            records.Select(r => r.ChannelId).Distinct().Count(),
            captured.Count > 0 ? captured.Min() : null,
            captured.Count > 0 ? captured.Max() : null,
            records.Count(r => r.Snapshots.Count == 0));

        VideoAnalyticsInput ToInput(StoredVideoRecord r)
        {
            var latest = r.Snapshots.Count > 0 ? r.Snapshots[^1] : (Observation?)null;
            return new VideoAnalyticsInput(
                r.VideoId,
                r.Title,
                r.PublishedAtUtc,
                r.DurationSeconds is { } d ? TimeSpan.FromSeconds(d) : null,
                r.LiveBroadcastContent,
                latest?.Views,
                null,
                null,
                latest?.AtUtc ?? now,
                r.Snapshots.Count > 1 ? r.Snapshots.Take(r.Snapshots.Count - 1).ToList() : []);
        }
    }
}
