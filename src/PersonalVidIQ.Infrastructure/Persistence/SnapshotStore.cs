using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PersonalVidIQ.Application.Analytics;
using PersonalVidIQ.Application.DTOs.YouTube;
using PersonalVidIQ.Application.Interfaces;
using PersonalVidIQ.Application.Options;
using PersonalVidIQ.Domain.Entities;
using PersonalVidIQ.Domain.Enums;

namespace PersonalVidIQ.Infrastructure.Persistence;

public sealed class SnapshotStore(
    IDbContextFactory<PersonalVidIQDbContext> contextFactory,
    IOptions<DataCollectionOptions> options) : ISnapshotStore
{
    private TimeSpan Spacing => TimeSpan.FromMinutes(options.Value.MinSnapshotSpacingMinutes);

    public async Task<int> AddVideoSnapshotsAsync(IReadOnlyCollection<VideoData> videos, CancellationToken cancellationToken)
    {
        if (videos.Count == 0)
        {
            return 0;
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var ids = videos.Select(v => v.Id).Distinct().ToList();
        var stored = await db.Videos.Where(v => ids.Contains(v.YouTubeVideoId))
            .Select(v => new { v.Id, v.YouTubeVideoId, Latest = v.Snapshots.Max(s => (DateTimeOffset?)s.CapturedAtUtc) })
            .ToDictionaryAsync(v => v.YouTubeVideoId, StringComparer.Ordinal, cancellationToken);

        var written = 0;
        foreach (var data in videos.GroupBy(v => v.Id).Select(g => g.Last()))
        {
            if (!stored.TryGetValue(data.Id, out var video) || TooSoon(video.Latest, data.RetrievedAtUtc))
            {
                continue;
            }

            db.VideoSnapshots.Add(new VideoSnapshot
            {
                VideoId = video.Id,
                ViewCount = data.ViewCount,
                LikeCount = data.LikeCount,
                CommentCount = data.CommentCount,
                CapturedAtUtc = data.RetrievedAtUtc,
                DataSource = MetricSource.YouTubeApi,
            });
            written++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return written;
    }

    public async Task<int> AddChannelSnapshotsAsync(IReadOnlyCollection<ChannelData> channels, CancellationToken cancellationToken)
    {
        if (channels.Count == 0)
        {
            return 0;
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var ids = channels.Select(c => c.Id).Distinct().ToList();
        var stored = await db.Channels.Where(c => ids.Contains(c.YouTubeChannelId))
            .Select(c => new { c.Id, c.YouTubeChannelId, Latest = c.Snapshots.Max(s => (DateTimeOffset?)s.CapturedAtUtc) })
            .ToDictionaryAsync(c => c.YouTubeChannelId, StringComparer.Ordinal, cancellationToken);

        var written = 0;
        foreach (var data in channels.GroupBy(c => c.Id).Select(g => g.Last()))
        {
            if (!stored.TryGetValue(data.Id, out var channel) || TooSoon(channel.Latest, data.RetrievedAtUtc))
            {
                continue;
            }

            db.ChannelSnapshots.Add(new ChannelSnapshot
            {
                ChannelId = channel.Id,
                SubscriberCount = data.SubscriberCount,
                SubscriberCountHidden = data.SubscriberCountHidden,
                ViewCount = data.ViewCount,
                VideoCount = data.VideoCount,
                CapturedAtUtc = data.RetrievedAtUtc,
                DataSource = MetricSource.YouTubeApi,
            });
            written++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return written;
    }

    public async Task<IReadOnlyList<VideoSnapshot>> GetVideoSnapshotsAsync(string youTubeVideoId, DateTimeOffset? sinceUtc, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.VideoSnapshots.AsNoTracking().Where(s => s.Video!.YouTubeVideoId == youTubeVideoId);
        if (sinceUtc is { } since)
        {
            query = query.Where(s => s.CapturedAtUtc >= since);
        }

        return await query.OrderBy(s => s.CapturedAtUtc).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<Observation>>> GetVideoHistoriesAsync(
        IReadOnlyCollection<string> youTubeVideoIds, DateTimeOffset? sinceUtc, CancellationToken cancellationToken)
    {
        if (youTubeVideoIds.Count == 0)
        {
            return new Dictionary<string, IReadOnlyList<Observation>>();
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var ids = youTubeVideoIds.Distinct().ToList();
        var query = db.VideoSnapshots.AsNoTracking().Where(s => ids.Contains(s.Video!.YouTubeVideoId));
        if (sinceUtc is { } since)
        {
            query = query.Where(s => s.CapturedAtUtc >= since);
        }

        var rows = await query
            .Select(s => new { s.Video!.YouTubeVideoId, s.CapturedAtUtc, s.ViewCount })
            .OrderBy(s => s.CapturedAtUtc)
            .ToListAsync(cancellationToken);

        return rows.GroupBy(r => r.YouTubeVideoId, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<Observation>)g.Select(r => new Observation(r.CapturedAtUtc, r.ViewCount)).ToList(),
                StringComparer.Ordinal);
    }

    public async Task<IReadOnlyList<ChannelSnapshot>> GetChannelSnapshotsAsync(string youTubeChannelId, DateTimeOffset? sinceUtc, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.ChannelSnapshots.AsNoTracking().Where(s => s.Channel!.YouTubeChannelId == youTubeChannelId);
        if (sinceUtc is { } since)
        {
            query = query.Where(s => s.CapturedAtUtc >= since);
        }

        return await query.OrderBy(s => s.CapturedAtUtc).ToListAsync(cancellationToken);
    }

    // Skips a capture that is no newer than the latest one, or within the minimum spacing after it.
    private bool TooSoon(DateTimeOffset? latest, DateTimeOffset capturedAt) =>
        latest is { } last && capturedAt < last + Spacing;
}
