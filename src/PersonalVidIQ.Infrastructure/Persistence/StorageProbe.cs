using Microsoft.EntityFrameworkCore;
using PersonalVidIQ.Application.DTOs;

namespace PersonalVidIQ.Infrastructure.Persistence;

public interface IStorageProbe
{
    Task<StorageStatus> GetStorageAsync(CancellationToken cancellationToken);
}

public sealed class StorageProbe(IDbContextFactory<PersonalVidIQDbContext> contextFactory) : IStorageProbe
{
    public async Task<StorageStatus> GetStorageAsync(CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count > 0)
        {
            return new StorageStatus(pending, 0, 0, 0, 0, 0, null);
        }

        var oldestVideo = await db.VideoSnapshots.MinAsync(s => (DateTimeOffset?)s.CapturedAtUtc, cancellationToken);
        var oldestChannel = await db.ChannelSnapshots.MinAsync(s => (DateTimeOffset?)s.CapturedAtUtc, cancellationToken);

        return new StorageStatus(
            pending,
            await db.Channels.CountAsync(c => c.IsTracked, cancellationToken),
            await db.Videos.CountAsync(v => v.IsTracked, cancellationToken),
            await db.Videos.CountAsync(cancellationToken),
            await db.VideoSnapshots.CountAsync(cancellationToken),
            await db.ChannelSnapshots.CountAsync(cancellationToken),
            new[] { oldestVideo, oldestChannel }.Where(d => d.HasValue).Min());
    }
}
