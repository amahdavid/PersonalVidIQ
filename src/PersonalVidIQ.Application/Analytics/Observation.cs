namespace PersonalVidIQ.Application.Analytics;

/// <summary>A view count seen at a moment (a stored snapshot or a fresh API read).</summary>
public readonly record struct Observation(DateTimeOffset AtUtc, long? Views);
