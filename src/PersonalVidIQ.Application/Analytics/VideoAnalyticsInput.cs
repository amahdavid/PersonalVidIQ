namespace PersonalVidIQ.Application.Analytics;

/// <summary>Everything the calculators need about one video: its current public counts and stored history.</summary>
public sealed record VideoAnalyticsInput(
    string VideoId,
    string Title,
    DateTimeOffset? PublishedAtUtc,
    TimeSpan? Duration,
    string? LiveBroadcastContent,
    long? Views,
    long? Likes,
    long? Comments,
    DateTimeOffset CapturedAtUtc,
    IReadOnlyList<Observation> History)
{
    /// <summary>Stored snapshots plus the current read.</summary>
    public IEnumerable<Observation> AllObservations => History.Append(new Observation(CapturedAtUtc, Views));

    /// <summary>Live streams and upcoming premieres don't behave like uploads and are kept out of cohorts.</summary>
    public bool IsLiveOrUpcoming => LiveBroadcastContent is "live" or "upcoming";
}
