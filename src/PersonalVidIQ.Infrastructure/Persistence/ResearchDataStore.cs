using Microsoft.EntityFrameworkCore;
using PersonalVidIQ.Application.Analytics;
using PersonalVidIQ.Application.Interfaces;

namespace PersonalVidIQ.Infrastructure.Persistence;

public sealed class ResearchDataStore(IDbContextFactory<PersonalVidIQDbContext> contextFactory) : IResearchDataStore
{
    public async Task<IReadOnlyList<StoredVideoRecord>> GetChannelVideosAsync(
        IReadOnlyCollection<string>? channelIds, DateTimeOffset snapshotsSinceUtc, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var videos = db.Videos.AsNoTracking().Where(v => v.Channel != null);
        if (channelIds is null)
        {
            videos = videos.Where(v => v.Channel!.IsTracked);
        }
        else
        {
            var ids = channelIds.ToList();
            videos = videos.Where(v => ids.Contains(v.Channel!.YouTubeChannelId));
        }

        var rows = await videos
            .Select(v => new
            {
                v.YouTubeVideoId,
                ChannelId = v.Channel!.YouTubeChannelId,
                ChannelTitle = v.Channel.Title,
                v.Title,
                v.Tags,
                v.PublishedAtUtc,
                v.DurationSeconds,
                v.LiveBroadcastContent,
                Snapshots = v.Snapshots
                    .Where(s => s.CapturedAtUtc >= snapshotsSinceUtc)
                    .OrderBy(s => s.CapturedAtUtc)
                    .Select(s => new { s.CapturedAtUtc, s.ViewCount })
                    .ToList(),
            })
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return rows.Select(r => new StoredVideoRecord(
                r.YouTubeVideoId, r.ChannelId, r.ChannelTitle, r.Title, r.Tags, r.PublishedAtUtc, r.DurationSeconds, r.LiveBroadcastContent,
                r.Snapshots.Select(s => new Observation(s.CapturedAtUtc, s.ViewCount)).ToList()))
            .ToList();
    }
}
