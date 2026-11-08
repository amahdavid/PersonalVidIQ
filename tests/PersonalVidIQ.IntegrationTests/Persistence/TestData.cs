using PersonalVidIQ.Application.DTOs.YouTube;

namespace PersonalVidIQ.IntegrationTests.Persistence;

internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    public static ChannelData Channel(string id = "UCaaaaaaaaaaaaaaaaaaaaaa", DateTimeOffset? at = null, long? subs = 1_230_000, bool hidden = false) => new(
        id, "Football Facts", "Daily football Shorts.", "@footballfacts", "https://yt3.ggpht.com/a.jpg",
        new DateTimeOffset(2021, 3, 4, 10, 0, 0, TimeSpan.Zero), "GB", "UU" + id[2..],
        hidden ? null : subs, hidden, 123_456_789, 842, at ?? Now);

    public static VideoData Video(string id, string channelId = "UCaaaaaaaaaaaaaaaaaaaaaa", DateTimeOffset? at = null, long? views = 1000, int seconds = 58) => new(
        id, channelId, "Football Facts", $"Video {id}", "desc", Now.AddDays(-2), TimeSpan.FromSeconds(seconds),
        $"https://i.ytimg.com/vi/{id}/hqdefault.jpg", ["messi", "football"], "17", "none",
        views, 50, 5, at ?? Now);
}
