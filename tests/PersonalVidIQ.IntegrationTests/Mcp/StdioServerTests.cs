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
        // Different working directory on purpose: config must load from next to the binary.
        WorkingDirectory = Path.GetTempPath(),
        EnvironmentVariables = new Dictionary<string, string?>
        {
            ["Database__ConnectionString"] = TestSettings.ConnectionString,
        },
    });

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
}
