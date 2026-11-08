using System.Globalization;

namespace PersonalVidIQ.Application.Analytics;

public sealed record VelocityResult(
    CalculatedMetric Metric,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    long? ViewsGained,
    double? ElapsedHours);

/// <summary>
/// viewsPerDay = (latestViews − earlierViews) ÷ elapsedDays, using real elapsed time between the two
/// observations (collection is on demand, so gaps are normal).
/// </summary>
public static class ViewVelocity
{
    public const string Formula = "(latest views − earlier views) ÷ days between the two observations";

    /// <param name="observations">Snapshots and/or a fresh read, any order. Null counts are ignored.</param>
    /// <param name="window">
    /// Look-back from the latest observation. The baseline is the newest observation at least this old;
    /// if none is that old, the oldest one is used and a note says the window is shorter than asked.
    /// Null uses the whole history.
    /// </param>
    public static VelocityResult Calculate(IEnumerable<Observation> observations, TimeSpan? window, string name = "views_per_day")
    {
        var points = observations
            .Where(o => o.Views is not null)
            .GroupBy(o => o.AtUtc)
            .Select(g => g.Last())
            .OrderBy(o => o.AtUtc)
            .ToList();

        if (points.Count < 2)
        {
            return Unavailable(name, $"Needs at least 2 view counts taken at different times; have {points.Count}. Track the video and refresh it later.", points.Count);
        }

        var latest = points[^1];
        var notes = new List<string>();
        Observation baseline;
        if (window is { } w)
        {
            var cutoff = latest.AtUtc - w;
            var candidates = points.Where(p => p.AtUtc <= cutoff).ToList();
            if (candidates.Count > 0)
            {
                baseline = candidates[^1];
            }
            else
            {
                baseline = points[0];
                notes.Add($"History covers {Hours(latest.AtUtc - baseline.AtUtc)} h, shorter than the {Hours(w)} h window asked for.");
            }
        }
        else
        {
            baseline = points[0];
        }

        var elapsed = latest.AtUtc - baseline.AtUtc;
        if (elapsed <= TimeSpan.Zero)
        {
            return Unavailable(name, "The observations have no time between them.", points.Count);
        }

        var gained = latest.Views!.Value - baseline.Views!.Value;
        var perDay = gained / elapsed.TotalDays;
        var confidence = elapsed.TotalHours switch
        {
            < 1 => Confidence.Low,
            < 6 => Confidence.Medium,
            _ => Confidence.High,
        };

        if (gained < 0)
        {
            confidence = Confidence.Low;
            notes.Add("The view count went down between observations (YouTube sometimes removes invalid views).");
        }

        if (elapsed.TotalHours < 1)
        {
            notes.Add("Less than an hour between observations; the rate is noisy.");
        }

        var used = points.Count(p => p.AtUtc >= baseline.AtUtc);
        return new VelocityResult(
            new CalculatedMetric(
                name,
                Statistics.Round(perDay),
                "views/day",
                confidence,
                Formula,
                $"{baseline.AtUtc:u} → {latest.AtUtc:u} ({Hours(elapsed)} h)",
                used,
                notes),
            baseline.AtUtc,
            latest.AtUtc,
            gained,
            Statistics.Round(elapsed.TotalHours));
    }

    private static VelocityResult Unavailable(string name, string reason, int count) =>
        new(CalculatedMetric.Unavailable(name, "views/day", Formula, reason, count), null, null, null, null);

    private static string Hours(TimeSpan span) => Statistics.Round(span.TotalHours, 1).ToString(CultureInfo.InvariantCulture);
}
