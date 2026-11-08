namespace PersonalVidIQ.Application.Analytics;

/// <summary>
/// Lifetime views per day since publish: the day-one metric that works without any stored history
/// (DECISIONS.md, "Outliers before any history exists").
/// </summary>
public static class LifetimeAverage
{
    public const string Formula = "current views ÷ days since publish (minimum 1 hour)";

    private static readonly TimeSpan MinimumAge = TimeSpan.FromHours(1);

    public static CalculatedMetric ViewsPerDay(long? views, DateTimeOffset? publishedAtUtc, DateTimeOffset asOfUtc)
    {
        const string name = "lifetime_views_per_day";
        if (views is null)
        {
            return CalculatedMetric.Unavailable(name, "views/day", Formula, "View count is hidden or unavailable.");
        }

        if (publishedAtUtc is null)
        {
            return CalculatedMetric.Unavailable(name, "views/day", Formula, "Publish date is unavailable.");
        }

        var age = asOfUtc - publishedAtUtc.Value;
        var notes = new List<string>();
        if (age < MinimumAge)
        {
            notes.Add("Published less than an hour ago; age treated as 1 hour.");
            age = MinimumAge;
        }

        var confidence = age.TotalHours < 24 ? Confidence.Low : Confidence.Medium;
        notes.Add(age.TotalHours < 24
            ? "Under a day old: early views come in bursts, so this average swings a lot."
            : "A lifetime average: it hides whether views are speeding up or slowing down.");

        return new CalculatedMetric(
            name,
            Statistics.Round(views.Value / age.TotalDays),
            "views/day",
            confidence,
            Formula,
            $"{publishedAtUtc.Value:u} → {asOfUtc:u}",
            1,
            notes);
    }

    public static double AgeDays(DateTimeOffset publishedAtUtc, DateTimeOffset asOfUtc) =>
        Math.Max(MinimumAge.TotalDays, (asOfUtc - publishedAtUtc).TotalDays);
}
