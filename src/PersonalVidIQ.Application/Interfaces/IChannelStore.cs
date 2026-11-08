using PersonalVidIQ.Application.DTOs.YouTube;
using PersonalVidIQ.Domain.Entities;

namespace PersonalVidIQ.Application.Interfaces;

public interface IChannelStore
{
    /// <summary>Inserts or updates channel metadata from YouTube and stamps LastRefreshedAtUtc. Tracking state is kept.</summary>
    Task<Channel> UpsertAsync(ChannelData data, CancellationToken cancellationToken);

    Task<Channel?> FindAsync(string youTubeChannelId, CancellationToken cancellationToken);

    /// <summary>Marks a stored channel tracked or untracked. Returns false when the channel isn't stored.</summary>
    Task<bool> SetTrackedAsync(string youTubeChannelId, bool tracked, CancellationToken cancellationToken);

    Task<IReadOnlyList<Channel>> ListTrackedAsync(CancellationToken cancellationToken);

    Task MarkUploadsCheckedAsync(string youTubeChannelId, CancellationToken cancellationToken);
}
