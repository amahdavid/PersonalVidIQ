using System.Globalization;
using PersonalVidIQ.Application.YouTube;

namespace PersonalVidIQ.Application.Analytics;

public enum OutlierMethod
{
    /// <summary>Lifetime views per day since publish. Works on day one, no history needed.</summary>
    LifetimeAverage,

    /// <summary>Recent views per day from stored snapshots. Needs history for the video and its cohort.</summary>
    SnapshotVelocity,
}

public enum OutlierBand
{
    Unavailable,
    Below,
    Typical,
    Above,
    WellAbove,
}

public sealed record OutlierSettings(
    int ShortsMaxDurationSeconds = 180,
    int MinCohortSize = 5,
    int HighConfidenceCohortSize = 10,
    double VelocityWindowDays = 7,
    double MinVelocityHours = 12);

public sealed record OutlierResult(
    string VideoId,
    string Title,
    VideoFormat Format,
    double? AgeDays,
    OutlierMethod? Method,
    double? VideoValue,
    double? CohortMedian,
    double? Ratio,
    OutlierBand Band,
    int CohortSize,
    double? CohortMinAgeDays,
    double? CohortMaxAgeDays,
    bool CohortWidened,
    Confidence Confidence,
    string Formula,
    IReadOnlyList<string> Notes);

/// <summary>
/// Channel-relative outlier score: the video's views/day ÷ the median views/day of the same channel's
/// videos of the same format (Short vs long-form) and similar age. DECISIONS.md #5, #6, #9.
/// </summary>
public static class OutlierScorer
{
    public const string Formula =
        "video views/day ÷ median views/day of same-channel, same-format videos aged 0.5×–2× the video's age (widened to 0.25×–4× if too few)";

    public static IReadOnlyList<OutlierResult> ScoreAll(IReadOnlyList<VideoAnalyticsInput> channelVideos, OutlierSettings settings, DateTimeOffset asOfUtc) =>
        channelVideos.Select(v => Score(v, channelVideos, settings, asOfUtc)).ToList();

    /// <param name="channelVideos">Videos from the same channel; the target is excluded automatically.</param>
    public static OutlierResult Score(VideoAnalyticsInput target, IReadOnlyList<VideoAnalyticsInput> channelVideos, OutlierSettings settings, DateTimeOffset asOfUtc)
    {
        var format = VideoFormatClassifier.Classify(target.Duration, settings.ShortsMaxDurationSeconds);
        if (target.IsLiveOrUpcoming)
        {
            return Unavailable(target, format, null, "Live streams and premieres aren't scored.");
        }

        if (format == VideoFormat.Unknown)
        {
            return Unavailable(target, format, null, "Duration is unknown, so it can't be grouped as a Short or long-form video.");
        }

        if (target.PublishedAtUtc is null || target.Views is null)
        {
            return Unavailable(target, format, null, "Publish date or view count is unavailable.");
        }

        var age = LifetimeAverage.AgeDays(target.PublishedAtUtc.Value, asOfUtc);
        var effectiveAge = Math.Max(1.0, age);

        var sameFormat = channelVideos
            .Where(v => v.VideoId != target.VideoId
                && !v.IsLiveOrUpcoming
                && v.PublishedAtUtc is not null
                && v.Views is not null
                && VideoFormatClassifier.Classify(v.Duration, settings.ShortsMaxDurationSeconds) == format)
            .Select(v => (Video: v, Age: LifetimeAverage.AgeDays(v.PublishedAtUtc!.Value, asOfUtc)))
            .ToList();

        var widened = false;
        var cohort = InBand(sameFormat, effectiveAge, 0.5, 2.0);
        if (cohort.Count < settings.MinCohortSize)
        {
            cohort = InBand(sameFormat, effectiveAge, 0.25, 4.0);
            widened = true;
        }

        var formatName = format == VideoFormat.Short ? "Shorts" : "long-form videos";
        if (cohort.Count < settings.MinCohortSize)
        {
            return Unavailable(target, format, age,
                $"Only {cohort.Count} other {formatName} from this channel are a similar age (need {settings.MinCohortSize}). " +
                $"{sameFormat.Count} {formatName} were checked in total.",
                cohort.Count);
        }

        var notes = new List<string>();
        var window = TimeSpan.FromDays(settings.VelocityWindowDays);

        // Prefer recent velocity when the target and enough of its cohort have usable history.
        var targetVelocity = UsableVelocity(target, window, settings);
        var cohortVelocities = cohort
            .Select(c => UsableVelocity(c.Video, window, settings))
            .Where(v => v is not null)
            .Select(v => v!.Value)
            .ToList();

        OutlierMethod method;
        double videoValue;
        List<double> cohortValues;
        if (targetVelocity is { } tv && cohortVelocities.Count >= settings.MinCohortSize)
        {
            method = OutlierMethod.SnapshotVelocity;
            videoValue = tv;
            cohortValues = cohortVelocities;
            notes.Add($"Uses recent views/day from stored snapshots (up to {settings.VelocityWindowDays.ToString(CultureInfo.InvariantCulture)} days).");
        }
        else
        {
            method = OutlierMethod.LifetimeAverage;
            videoValue = target.Views.Value / age;
            cohortValues = cohort.Select(c => c.Video.Views!.Value / c.Age).ToList();
            notes.Add("Uses lifetime views/day since publish (not enough stored history for recent velocity).");
        }

        var median = Statistics.Median(cohortValues)!.Value;
        if (median <= 0)
        {
            return Unavailable(target, format, age, "The comparison group's median is zero views/day.", cohortValues.Count, method);
        }

        var ratio = videoValue / median;
        var confidence = cohortValues.Count >= settings.HighConfidenceCohortSize && !widened ? Confidence.High : Confidence.Medium;
        if (method == OutlierMethod.LifetimeAverage && age < 1)
        {
            confidence = Confidence.Low;
            notes.Add("Under a day old: early views come in bursts, so the score can swing a lot.");
        }

        if (widened)
        {
            notes.Add("Comparison group widened to videos aged 0.25×–4× this one's age.");
        }

        return new OutlierResult(
            target.VideoId,
            target.Title,
            format,
            Statistics.Round(age),
            method,
            Statistics.Round(videoValue),
            Statistics.Round(median),
            Statistics.Round(ratio),
            BandFor(ratio),
            cohortValues.Count,
            Statistics.Round(cohort.Min(c => c.Age)),
            Statistics.Round(cohort.Max(c => c.Age)),
            widened,
            confidence,
            Formula,
            notes);
    }

    public static OutlierBand BandFor(double ratio) => ratio switch
    {
        >= 3 => OutlierBand.WellAbove,
        >= 1.5 => OutlierBand.Above,
        > 2.0 / 3.0 => OutlierBand.Typical,
        _ => OutlierBand.Below,
    };

    private static List<(VideoAnalyticsInput Video, double Age)> InBand(
        List<(VideoAnalyticsInput Video, double Age)> candidates, double age, double low, double high) =>
        candidates.Where(c => Math.Max(1.0, c.Age) >= age * low && Math.Max(1.0, c.Age) <= age * high).ToList();

    private static double? UsableVelocity(VideoAnalyticsInput video, TimeSpan window, OutlierSettings settings)
    {
        var velocity = ViewVelocity.Calculate(video.AllObservations, window);
        return velocity.Metric.Value is { } value
            && velocity.ElapsedHours >= settings.MinVelocityHours
            && velocity.Metric.Confidence >= Confidence.Medium
            ? value
            : null;
    }

    private static OutlierResult Unavailable(VideoAnalyticsInput target, VideoFormat format, double? age, string reason, int cohortSize = 0, OutlierMethod? method = null) =>
        new(target.VideoId, target.Title, format, age is null ? null : Statistics.Round(age.Value), method, null, null, null,
            OutlierBand.Unavailable, cohortSize, null, null, false, Confidence.Unavailable, Formula, [reason]);
}
