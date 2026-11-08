namespace PersonalVidIQ.Application.Services;

internal static class Limitations
{
    public const string NoPrivateMetrics =
        "Impressions, click-through rate, watch time and retention are private to each channel's owner and are not available.";

    public static string Retention(int days) =>
        $"History is limited to the last {days} days (YouTube API policy), and only exists for videos PersonalVidIQ has stored.";

    public const string Calculated = "Numbers under 'calculated' are worked out by PersonalVidIQ, not reported by YouTube.";
}
