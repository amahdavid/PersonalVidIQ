using PersonalVidIQ.Application.YouTube;

namespace PersonalVidIQ.UnitTests.YouTube;

public sealed class VideoFormatClassifierTests
{
    [Theory]
    [InlineData(58, VideoFormat.Short)]
    [InlineData(180, VideoFormat.Short)]
    [InlineData(181, VideoFormat.LongForm)]
    [InlineData(0, VideoFormat.Unknown)]
    public void Classifies_by_duration(int seconds, VideoFormat expected) =>
        Assert.Equal(expected, VideoFormatClassifier.Classify(TimeSpan.FromSeconds(seconds), 180));

    [Fact]
    public void Missing_duration_is_unknown() =>
        Assert.Equal(VideoFormat.Unknown, VideoFormatClassifier.Classify(null, 180));
}
