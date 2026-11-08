using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;

namespace PersonalVidIQ.Mcp;

public static class DependencyInjection
{
    /// <summary>MCP defaults plus enums as readable strings ("WellAbove", not 4).</summary>
    public static JsonSerializerOptions SerializerOptions { get; } = CreateSerializerOptions();

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(McpJsonUtilities.DefaultOptions);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        return options;
    }

    /// <summary>Registers the MCP server over stdio with every tool in this assembly.</summary>
    public static IServiceCollection AddPersonalVidIQMcpServer(this IServiceCollection services)
    {
        services.AddMcpServer(options =>
            {
                options.ServerInfo = new() { Name = "personalvidiq", Version = typeof(DependencyInjection).Assembly.GetName().Version?.ToString(3) ?? "0.0.0" };
            })
            .WithStdioServerTransport()
            .WithToolsFromAssembly(typeof(DependencyInjection).Assembly, SerializerOptions);

        return services;
    }
}
