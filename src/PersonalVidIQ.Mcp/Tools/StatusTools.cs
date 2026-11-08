using System.ComponentModel;
using ModelContextProtocol.Server;
using PersonalVidIQ.Application.DTOs;
using PersonalVidIQ.Application.Interfaces;

namespace PersonalVidIQ.Mcp.Tools;

[McpServerToolType]
public sealed class StatusTools(ISystemStatusService statusService)
{
    [McpServerTool(Name = "get_status", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Check that PersonalVidIQ is set up: whether the YouTube API key is configured, whether the PostgreSQL database is reachable, and the active settings (snapshot retention, Shorts duration rule, search cap), and YouTube API quota used today by this server process. Lists any problems with how to fix them.")]
    public Task<SystemStatus> GetStatus(CancellationToken cancellationToken) =>
        statusService.GetStatusAsync(cancellationToken);
}
