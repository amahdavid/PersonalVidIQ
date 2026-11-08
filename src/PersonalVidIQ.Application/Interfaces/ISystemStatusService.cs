using PersonalVidIQ.Application.DTOs;

namespace PersonalVidIQ.Application.Interfaces;

/// <summary>Reports whether PersonalVidIQ is configured and its dependencies are reachable.</summary>
public interface ISystemStatusService
{
    Task<SystemStatus> GetStatusAsync(CancellationToken cancellationToken);
}
