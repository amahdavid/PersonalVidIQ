using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace PersonalVidIQ.IntegrationTests.Mcp;

/// <summary>Starts the real server the way Claude does (a child process over stdio) and calls it.</summary>
public sealed class StdioServerTests
{
    private static StdioClientTransport CreateTransport() => new(new StdioClientTransportOptions
    {
        Name = "personalvidiq",
        Command = "dotnet",
        Arguments = [Path.Combine(AppContext.BaseDirectory, "personalvidiq.dll"), "mcp"],
        WorkingDirectory = Path.GetTempPath(),
        EnvironmentVariables = OfflineEnvironment(),
    });

    private static Dictionary<string, string?> OfflineEnvironment() => new()
    {
        ["Database__ConnectionString"] = TestSettings.ConnectionString,
        // Never reach YouTube from these tests, even when user secrets hold a real key:
        // point the client at a closed local port and don't retry.
        ["YouTube__BaseUrl"] = "http://127.0.0.1:9/",
        ["YouTube__MaxRetries"] = "0",
    };

    [Fact]
    public async Task Server_lists_tools_and_answers_get_status()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var client = await McpClient.CreateAsync(CreateTransport(), cancellationToken: ct);

        var tools = await client.ListToolsAsync(cancellationToken: ct);
        Assert.Contains(tools, t => t.Name == "get_status");

        var result = await client.CallToolAsync("get_status", cancellationToken: ct);
        Assert.NotEqual(true, result.IsError);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        using var json = JsonDocument.Parse(text);
        Assert.True(json.RootElement.GetProperty("database").GetProperty("reachable").GetBoolean());
        Assert.Equal(30, json.RootElement.GetProperty("settings").GetProperty("snapshotRetentionDays").GetInt32());
    }

    [Fact]
    public async Task Server_exposes_every_mvp_tool()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var client = await McpClient.CreateAsync(CreateTransport(), cancellationToken: ct);

        var names = (await client.ListToolsAsync(cancellationToken: ct)).Select(t => t.Name).Order().ToList();

        Assert.Equal(
            ["analyze_video", "compare_channels", "compare_videos", "find_outliers", "get_channel", "get_channel_videos", "get_status",
             "get_video", "list_tracked", "refresh_tracked", "search_youtube", "track_channel", "track_video", "untrack_channel", "untrack_video"],
            names);
    }

    [Theory]
    [InlineData("get_video", "video", "not a video!", "Invalid input")]
    [InlineData("get_video", "video", "aaaaaaaaaaa", "YouTube temporarily unavailable")]
    [InlineData("get_channel_videos", "order", "sideways", "Invalid order")]
    public async Task Tool_errors_reach_claude_as_readable_messages(string tool, string argument, string value, string expected)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var client = await McpClient.CreateAsync(CreateTransport(), cancellationToken: ct);
        var args = new Dictionary<string, object?> { [argument] = value };
        if (tool == "get_channel_videos")
        {
            args["channel"] = "@someone";
        }

        var result = await client.CallToolAsync(tool, args, cancellationToken: ct);

        Assert.True(result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains(expected, text, StringComparison.Ordinal);
    }

    /// <summary>Real YouTube through the real server. Skipped unless PERSONALVIDIQ_YOUTUBE_API_KEY is set. About 6 quota units.</summary>
    [Fact]
    public async Task Live_round_trip_through_mcp()
    {
        var key = Environment.GetEnvironmentVariable("PERSONALVIDIQ_YOUTUBE_API_KEY");
        if (string.IsNullOrWhiteSpace(key))
        {
            Assert.Skip("Set PERSONALVIDIQ_YOUTUBE_API_KEY to run the live MCP round trip.");
        }

        var ct = TestContext.Current.CancellationToken;
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "personalvidiq",
            Command = "dotnet",
            Arguments = [Path.Combine(AppContext.BaseDirectory, "personalvidiq.dll"), "mcp"],
            WorkingDirectory = Path.GetTempPath(),
            EnvironmentVariables = new Dictionary<string, string?>
            {
                ["Database__ConnectionString"] = TestSettings.ConnectionString,
                ["YouTube__ApiKey"] = key,
            },
        });
        await using var client = await McpClient.CreateAsync(transport, cancellationToken: ct);

        var channel = await client.CallToolAsync("get_channel", new Dictionary<string, object?> { ["channel"] = "@YouTube" }, cancellationToken: ct);
        Assert.NotEqual(true, channel.IsError);

        var outliers = await client.CallToolAsync("find_outliers",
            new Dictionary<string, object?> { ["channel"] = "@YouTube", ["publishedWithinDays"] = 365, ["minRatio"] = 0 }, cancellationToken: ct);
        Assert.NotEqual(true, outliers.IsError);
        using var json = JsonDocument.Parse(Assert.IsType<TextContentBlock>(Assert.Single(outliers.Content)).Text);
        Assert.True(json.RootElement.GetProperty("videosChecked").GetInt32() > 0);
    }

    [Fact]
    public async Task List_tracked_works_without_youtube()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var client = await McpClient.CreateAsync(CreateTransport(), cancellationToken: ct);

        var result = await client.CallToolAsync("list_tracked", cancellationToken: ct);

        Assert.NotEqual(true, result.IsError);
        using var json = JsonDocument.Parse(Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
        Assert.True(json.RootElement.TryGetProperty("channels", out _));
    }
}
