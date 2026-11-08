using PersonalVidIQ.Domain.Enums;

namespace PersonalVidIQ.Domain.Entities;

/// <summary>Video statistics at one moment. Deleted after the retention window (max 30 days).</summary>
public sealed class VideoSnapshot
{
    public long Id { get; set; }

    public long VideoId { get; set; }

    public Video? Video { get; set; }

    public long? ViewCount { get; set; }

    public long? LikeCount { get; set; }

    public long? CommentCount { get; set; }

    public DateTimeOffset CapturedAtUtc { get; set; }

    public MetricSource DataSource { get; set; } = MetricSource.YouTubeApi;
}
