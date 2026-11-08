using Microsoft.EntityFrameworkCore;
using PersonalVidIQ.Application.DTOs.YouTube;
using PersonalVidIQ.Application.Interfaces;
using PersonalVidIQ.Domain.Entities;
using PersonalVidIQ.Domain.Enums;

namespace PersonalVidIQ.Infrastructure.Persistence;

public sealed class VideoStore(IDbContextFactory<PersonalVidIQDbContext> contextFactory, TimeProvider timeProvider) : IVideoStore
{
    public async Task<IReadOnlyList<Video>> UpsertAsync(IReadOnlyCollection<VideoData> videos, TrackingSource? trackAs, CancellationToken cancellationToken)
    {
        if (videos.Count == 0)
        {
            return [];
        }

        var byId = videos.GroupBy(v => v.Id, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);

        for (var attempt = 0; ; attempt++)
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            var now = timeProvider.GetUtcNow();
            var ids = byId.Keys.ToList();
            var channelIds = byId.Values.Select(v => v.ChannelId).Where(id => id.Length > 0).Distinct().ToList();

            var existing = await db.Videos.Where(v => ids.Contains(v.YouTubeVideoId)).ToDictionaryAsync(v => v.YouTubeVideoId, StringComparer.Ordinal, cancellationToken);
            var channels = await db.Channels.Where(c => channelIds.Contains(c.YouTubeChannelId))
                .ToDictionaryAsync(c => c.YouTubeChannelId, c => c.Id, StringComparer.Ordinal, cancellationToken);

            var result = new List<Video>(byId.Count);
            foreach (var data in byId.Values)
            {
                if (!existing.TryGetValue(data.Id, out var video))
                {
                    video = new Video { YouTubeVideoId = data.Id, Title = data.Title, CreatedAtUtc = now };
                    db.Videos.Add(video);
                }

                video.Title = data.Title;
                video.Description = data.Description;
                video.PublishedAtUtc = data.PublishedAtUtc;
                video.DurationSeconds = data.Duration is { } d ? (int)Math.Round(d.TotalSeconds) : null;
                video.ThumbnailUrl = data.ThumbnailUrl;
                video.Tags = data.Tags.ToList();
                video.CategoryId = data.CategoryId;
                video.LiveBroadcastContent = data.LiveBroadcastContent;
                if (channels.TryGetValue(data.ChannelId, out var channelId))
                {
                    video.ChannelId = channelId;
                }

                if (trackAs is { } source && !video.IsTracked)
                {
                    video.IsTracked = true;
                    video.TrackingSource = source;
                    video.TrackedAtUtc = now;
                }

                video.LastRefreshedAtUtc = data.RetrievedAtUtc;
                video.UpdatedAtUtc = now;
                result.Add(video);
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return result;
            }
            catch (DbUpdateException ex) when (attempt == 0 && DbErrors.IsUniqueViolation(ex))
            {
                // Another process inserted one of these first; retry as updates.
            }
        }
    }

    public async Task<Video?> FindAsync(string youTubeVideoId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Videos.AsNoTracking().Include(v => v.Channel).SingleOrDefaultAsync(v => v.YouTubeVideoId == youTubeVideoId, cancellationToken);
    }

    public async Task<bool> SetTrackedAsync(string youTubeVideoId, bool tracked, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var video = await db.Videos.SingleOrDefaultAsync(v => v.YouTubeVideoId == youTubeVideoId, cancellationToken);
        if (video is null)
        {
            return false;
        }

        var now = timeProvider.GetUtcNow();
        if (tracked)
        {
            if (!video.IsTracked)
            {
                video.IsTracked = true;
                video.TrackingSource = TrackingSource.Manual;
                video.TrackedAtUtc = now;
            }
        }
        else
        {
            video.IsTracked = false;
            video.TrackingSource = null;
            video.TrackedAtUtc = null;
        }

        video.UpdatedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> UntrackChannelVideosAsync(string youTubeChannelId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        return await db.Videos
            .Where(v => v.Channel!.YouTubeChannelId == youTubeChannelId && v.IsTracked && v.TrackingSource == TrackingSource.Channel)
            .ExecuteUpdateAsync(
                s => s.SetProperty(v => v.IsTracked, false)
                    .SetProperty(v => v.TrackingSource, (TrackingSource?)null)
                    .SetProperty(v => v.TrackedAtUtc, (DateTimeOffset?)null)
                    .SetProperty(v => v.UpdatedAtUtc, now),
                cancellationToken);
    }

    public async Task<IReadOnlyList<Video>> ListTrackedAsync(CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Videos.AsNoTracking().Include(v => v.Channel).Where(v => v.IsTracked)
            .OrderByDescending(v => v.PublishedAtUtc).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Video>> ListByChannelAsync(string youTubeChannelId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Videos.AsNoTracking().Where(v => v.Channel!.YouTubeChannelId == youTubeChannelId)
            .OrderByDescending(v => v.PublishedAtUtc).ToListAsync(cancellationToken);
    }
}
