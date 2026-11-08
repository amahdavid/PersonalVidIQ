namespace PersonalVidIQ.Application.YouTube;

public enum VideoFormat
{
    Short,
    LongForm,
    Unknown,
}

/// <summary>
/// The API has no "is Short" flag, so format is inferred from duration (DECISIONS.md #9).
/// A calculated classification, not a YouTube field.
/// </summary>
public static class VideoFormatClassifier
{
    public static VideoFormat Classify(TimeSpan? duration, int shortsMaxDurationSeconds) => duration switch
    {
        null => VideoFormat.Unknown,
        { TotalSeconds: <= 0 } => VideoFormat.Unknown, // live streams and premieres report P0D
        var d when d.Value.TotalSeconds <= shortsMaxDurationSeconds => VideoFormat.Short,
        _ => VideoFormat.LongForm,
    };
}
