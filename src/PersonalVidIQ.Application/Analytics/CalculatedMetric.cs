using PersonalVidIQ.Domain.Enums;

namespace PersonalVidIQ.Application.Analytics;

/// <summary>
/// A number PersonalVidIQ calculated (never a YouTube metric), with everything needed to judge it:
/// formula, data window, sample size, confidence and caveats. YouTube API Developer Policies III.E.4.h.
/// </summary>
public sealed record CalculatedMetric(
    string Name,
    double? Value,
    string Unit,
    Confidence Confidence,
    string Formula,
    string? Window,
    int SampleSize,
    IReadOnlyList<string> Notes)
{
    public const string Disclaimer = "Calculated by PersonalVidIQ from public YouTube data; not a YouTube metric.";

    public MetricSource Source => Value is null ? MetricSource.Unavailable : MetricSource.Calculated;

    public static CalculatedMetric Unavailable(string name, string unit, string formula, string reason, int sampleSize = 0) =>
        new(name, null, unit, Confidence.Unavailable, formula, null, sampleSize, [reason]);
}
