using PersonalVidIQ.Domain.Enums;

namespace PersonalVidIQ.Domain.Entities;

/// <summary>Channel statistics at one moment. Deleted after the retention window (max 30 days).</summary>
public sealed class ChannelSnapshot
{
    public long Id { get; set; }

    public long ChannelId { get; set; }

    public Channel? Channel { get; set; }

    /// <summary>Rounded by YouTube to 3 significant figures; null when hidden.</summary>
    public long? SubscriberCount { get; set; }

    public bool SubscriberCountHidden { get; set; }

    public long? ViewCount { get; set; }

    public long? VideoCount { get; set; }

    public DateTimeOffset CapturedAtUtc { get; set; }

    public MetricSource DataSource { get; set; } = MetricSource.YouTubeApi;
}
