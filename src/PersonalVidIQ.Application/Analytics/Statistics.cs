namespace PersonalVidIQ.Application.Analytics;

public static class Statistics
{
    /// <summary>Median of the values, or null when there are none.</summary>
    public static double? Median(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        if (sorted.Length == 0)
        {
            return null;
        }

        var mid = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }

    public static double Round(double value, int digits = 2) => Math.Round(value, digits, MidpointRounding.AwayFromZero);
}
