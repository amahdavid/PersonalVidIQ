namespace PersonalVidIQ.Domain.Enums;

/// <summary>Why a video is tracked.</summary>
public enum TrackingSource
{
    /// <summary>Tracked on its own with track_video.</summary>
    Manual,

    /// <summary>Tracked because its channel is tracked (recent or new upload).</summary>
    Channel,
}
