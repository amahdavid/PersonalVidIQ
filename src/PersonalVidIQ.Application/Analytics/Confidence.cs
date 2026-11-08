namespace PersonalVidIQ.Application.Analytics;

/// <summary>How much to trust a calculated number. Unavailable means no number was produced.</summary>
public enum Confidence
{
    Unavailable,
    Low,
    Medium,
    High,
}
