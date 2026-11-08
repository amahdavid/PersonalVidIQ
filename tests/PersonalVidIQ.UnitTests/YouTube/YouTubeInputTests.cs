using PersonalVidIQ.Application.YouTube;

namespace PersonalVidIQ.UnitTests.YouTube;

public sealed class YouTubeInputTests
{
    [Theory]
    [InlineData("UCaaaaaaaaaaaaaaaaaaaaaa", ChannelRefKind.Id, "UCaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("  @footballfacts ", ChannelRefKind.Handle, "@footballfacts")]
    [InlineData("https://www.youtube.com/@footballfacts", ChannelRefKind.Handle, "@footballfacts")]
    [InlineData("youtube.com/@footballfacts/shorts", ChannelRefKind.Handle, "@footballfacts")]
    [InlineData("https://m.youtube.com/@football.facts?si=abc", ChannelRefKind.Handle, "@football.facts")]
    [InlineData("https://www.youtube.com/channel/UCaaaaaaaaaaaaaaaaaaaaaa/videos", ChannelRefKind.Id, "UCaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("https://www.youtube.com/user/oldname", ChannelRefKind.Username, "oldname")]
    public void ParseChannel_accepts_ids_handles_and_urls(string input, ChannelRefKind kind, string value)
    {
        var parsed = YouTubeInput.ParseChannel(input);

        Assert.Equal(kind, parsed.Kind);
        Assert.Equal(value, parsed.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("footballfacts")]
    [InlineData("https://www.youtube.com/c/OldCustom")]
    [InlineData("https://www.youtube.com/watch?v=aaaaaaaaaaa")]
    [InlineData("https://example.com/@someone")]
    [InlineData("@a")]
    public void ParseChannel_rejects_unusable_input(string input)
    {
        var ex = Assert.Throws<YouTubeApiException>(() => YouTubeInput.ParseChannel(input));
        Assert.Equal(YouTubeErrorKind.InvalidInput, ex.Kind);
    }

    [Theory]
    [InlineData("aaaaaaaaaaa")]
    [InlineData("https://www.youtube.com/watch?v=aaaaaaaaaaa&t=30s")]
    [InlineData("https://youtu.be/aaaaaaaaaaa?si=xyz")]
    [InlineData("https://www.youtube.com/shorts/aaaaaaaaaaa")]
    [InlineData("youtube.com/shorts/aaaaaaaaaaa?feature=share")]
    [InlineData("https://www.youtube.com/live/aaaaaaaaaaa")]
    [InlineData("https://www.youtube.com/embed/aaaaaaaaaaa")]
    public void ParseVideoId_accepts_ids_and_urls(string input) =>
        Assert.Equal("aaaaaaaaaaa", YouTubeInput.ParseVideoId(input));

    [Theory]
    [InlineData("")]
    [InlineData("tooshort")]
    [InlineData("https://www.youtube.com/@footballfacts")]
    [InlineData("https://example.com/watch?v=aaaaaaaaaaa")]
    public void ParseVideoId_rejects_unusable_input(string input)
    {
        var ex = Assert.Throws<YouTubeApiException>(() => YouTubeInput.ParseVideoId(input));
        Assert.Equal(YouTubeErrorKind.InvalidInput, ex.Kind);
    }
}
