using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PersonalVidIQ.Application.Interfaces;
using PersonalVidIQ.Infrastructure;
using PersonalVidIQ.Mcp;

// Usage:
//   personalvidiq            start the MCP server on stdio (what Claude launches)
//   personalvidiq mcp        same as above
//   personalvidiq status     print the setup/status report and exit
var command = args.Length > 0 ? args[0].ToLowerInvariant() : "mcp";

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args.Skip(1).ToArray(),
    // Claude starts the server from an arbitrary working directory; load config next to the binary.
    ContentRootPath = AppContext.BaseDirectory,
});

builder.Configuration.AddUserSecrets<Program>(optional: true);

// stdout carries the MCP protocol, so every log line goes to stderr.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddPersonalVidIQInfrastructure(builder.Configuration);

switch (command)
{
    case "mcp":
        builder.Services.AddPersonalVidIQMcpServer();
        await builder.Build().RunAsync();
        return 0;

    case "status":
    {
        using var host = builder.Build();
        var status = await host.Services.GetRequiredService<ISystemStatusService>().GetStatusAsync(CancellationToken.None);
        Console.WriteLine(JsonSerializer.Serialize(status, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        return status.Problems.Count == 0 ? 0 : 1;
    }

    default:
        Console.Error.WriteLine($"Unknown command '{command}'. Use: mcp | status");
        return 2;
}

public partial class Program;
