using Microsoft.Extensions.DependencyInjection;

namespace PersonalVidIQ.Mcp;

public static class DependencyInjection
{
    /// <summary>Registers the MCP server over stdio with every tool in this assembly.</summary>
    public static IServiceCollection AddPersonalVidIQMcpServer(this IServiceCollection services)
    {
        services.AddMcpServer(options =>
            {
                options.ServerInfo = new() { Name = "personalvidiq", Version = typeof(DependencyInjection).Assembly.GetName().Version?.ToString(3) ?? "0.0.0" };
            })
            .WithStdioServerTransport()
            .WithToolsFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
