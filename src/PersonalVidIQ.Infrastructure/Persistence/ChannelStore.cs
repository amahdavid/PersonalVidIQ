using Microsoft.EntityFrameworkCore;
using PersonalVidIQ.Application.DTOs.YouTube;
using PersonalVidIQ.Application.Interfaces;
using PersonalVidIQ.Domain.Entities;

namespace PersonalVidIQ.Infrastructure.Persistence;

public sealed class ChannelStore(IDbContextFactory<PersonalVidIQDbContext> contextFactory, TimeProvider timeProvider) : IChannelStore
{
    public async Task<Channel> UpsertAsync(ChannelData data, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            var now = timeProvider.GetUtcNow();
            var channel = await db.Channels.SingleOrDefaultAsync(c => c.YouTubeChannelId == data.Id, cancellationToken);
            if (channel is null)
            {
                channel = new Channel { YouTubeChannelId = data.Id, Title = data.Title, CreatedAtUtc = now };
                db.Channels.Add(channel);
            }

            channel.Title = data.Title;
            channel.Description = data.Description;
            channel.CustomUrl = data.CustomUrl;
            channel.ThumbnailUrl = data.ThumbnailUrl;
            channel.PublishedAtUtc = data.PublishedAtUtc;
            channel.Country = data.Country;
            channel.UploadsPlaylistId = data.UploadsPlaylistId;
            channel.LastRefreshedAtUtc = data.RetrievedAtUtc;
            channel.UpdatedAtUtc = now;

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return channel;
            }
            catch (DbUpdateException ex) when (attempt == 0 && DbErrors.IsUniqueViolation(ex))
            {
                // Another process inserted it first; retry as an update.
            }
        }
    }

    public async Task<Channel?> FindAsync(string youTubeChannelId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Channels.AsNoTracking().SingleOrDefaultAsync(c => c.YouTubeChannelId == youTubeChannelId, cancellationToken);
    }

    public async Task<Channel?> FindByHandleAsync(string handle, CancellationToken cancellationToken)
    {
        var normalised = (handle.StartsWith('@') ? handle : "@" + handle).ToLowerInvariant();
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Channels.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CustomUrl != null && c.CustomUrl.ToLower() == normalised, cancellationToken);
    }

    public async Task<bool> SetTrackedAsync(string youTubeChannelId, bool tracked, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var channel = await db.Channels.SingleOrDefaultAsync(c => c.YouTubeChannelId == youTubeChannelId, cancellationToken);
        if (channel is null)
        {
            return false;
        }

        var now = timeProvider.GetUtcNow();
        if (tracked && !channel.IsTracked)
        {
            channel.TrackedAtUtc = now;
        }
        else if (!tracked)
        {
            channel.TrackedAtUtc = null;
        }

        channel.IsTracked = tracked;
        channel.UpdatedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<Channel>> ListTrackedAsync(CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Channels.AsNoTracking().Where(c => c.IsTracked).OrderBy(c => c.Title).ToListAsync(cancellationToken);
    }

    public async Task MarkUploadsCheckedAsync(string youTubeChannelId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        await db.Channels
            .Where(c => c.YouTubeChannelId == youTubeChannelId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.LastUploadsCheckAtUtc, now).SetProperty(c => c.UpdatedAtUtc, now), cancellationToken);
    }
}
