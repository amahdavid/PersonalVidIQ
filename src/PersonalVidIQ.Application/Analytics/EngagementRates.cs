namespace PersonalVidIQ.Application.Analytics;

public static class EngagementRates
{
    public static CalculatedMetric LikeRate(long? likes, long? views) => Rate("like_rate", "likes ÷ views", likes, views, "Likes are hidden on this video.");

    public static CalculatedMetric CommentRate(long? comments, long? views) => Rate("comment_rate", "comments ÷ views", comments, views, "Comments are disabled or hidden.");

    private static CalculatedMetric Rate(string name, string formula, long? part, long? views, string missingReason)
    {
        if (part is null)
        {
            return CalculatedMetric.Unavailable(name, "%", formula, missingReason);
        }

        if (views is null or 0)
        {
            return CalculatedMetric.Unavailable(name, "%", formula, "No views to divide by.");
        }

        var notes = new List<string>();
        var confidence = views < 1_000 ? Confidence.Low : Confidence.Medium;
        if (views < 1_000)
        {
            notes.Add("Fewer than 1,000 views; the rate is unstable.");
        }

        return new CalculatedMetric(name, Statistics.Round(100.0 * part.Value / views.Value, 3), "%", confidence, formula, null, 1, notes);
    }
}
