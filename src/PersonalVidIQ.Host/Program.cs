using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PersonalVidIQ.Application;
using PersonalVidIQ.Application.Interfaces;
using PersonalVidIQ.Infrastructure;
using PersonalVidIQ.Infrastructure.Persistence;
using PersonalVidIQ.Mcp;

// Usage:
//   personalvidiq            start the MCP server on stdio (what Claude launches)
//   personalvidiq mcp        same as above
//   personalvidiq status     print the setup/status report and exit
//   personalvidiq migrate    apply pending database migrations
//   personalvidiq purge      delete data past the 30-day retention window
//   personalvidiq collect [--force] [--dry-run]   refresh tracked items that are due
var command = args.Length > 0 ? args[0].ToLowerInvariant() : "mcp";
var flags = args.Skip(1).Where(a => a.StartsWith("--", StringComparison.Ordinal)).Select(a => a.ToLowerInvariant()).ToHashSet();

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args.Skip(1).Where(a => !a.StartsWith("--", StringComparison.Ordinal) || a.Contains('=')).ToArray(),
    // Claude starts the server from an arbitrary working directory; load config next to the binary.
    ContentRootPath = AppContext.BaseDirectory,
});

builder.Configuration.AddUserSecrets<Program>(optional: true);

// stdout carries the MCP protocol, so every log line goes to stderr.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddPersonalVidIQInfrastructure(builder.Configuration);
builder.Services.AddPersonalVidIQApplication();

var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

switch (command)
{
    case "mcp":
    {
        builder.Services.AddPersonalVidIQMcpServer();
        using var host = builder.Build();
        await DatabaseStartup.PrepareAsync(host.Services, CancellationToken.None);
        await host.RunAsync();
        return 0;
    }

    case "status":
    {
        using var host = builder.Build();
        var status = await host.Services.GetRequiredService<ISystemStatusService>().GetStatusAsync(CancellationToken.None);
        Console.WriteLine(JsonSerializer.Serialize(status, json));
        return status.Problems.Count == 0 ? 0 : 1;
    }

    case "migrate":
    {
        using var host = builder.Build();
        var applied = await host.Services.GetRequiredService<IDatabaseMigrator>().MigrateAsync(CancellationToken.None);
        Console.WriteLine(applied.Count == 0 ? "Database is up to date." : $"Applied: {string.Join(", ", applied)}");
        return 0;
    }

    case "purge":
    {
        using var host = builder.Build();
        var result = await host.Services.GetRequiredService<IRetentionService>().PurgeAsync(CancellationToken.None);
        Console.WriteLine(JsonSerializer.Serialize(result, json));
        return 0;
    }

    case "collect":
    {
        using var host = builder.Build();
        await DatabaseStartup.PrepareAsync(host.Services, CancellationToken.None);
        var report = await host.Services.GetRequiredService<PersonalVidIQ.Application.Interfaces.ICollectionService>()
            .CollectAsync(new PersonalVidIQ.Application.Collection.CollectRequest("cli", flags.Contains("--force"), flags.Contains("--dry-run")), CancellationToken.None);
        Console.WriteLine(JsonSerializer.Serialize(report, json));
        return report.Status is "Succeeded" or "Planned" ? 0 : 1;
    }

    default:
        Console.Error.WriteLine($"Unknown command '{command}'. Use: mcp | status | migrate | purge | collect [--force] [--dry-run]");
        return 2;
}

public partial class Program;
