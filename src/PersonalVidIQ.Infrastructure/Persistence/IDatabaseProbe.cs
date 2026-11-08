using PersonalVidIQ.Application.DTOs;

namespace PersonalVidIQ.Infrastructure.Persistence;

public interface IDatabaseProbe
{
    Task<DatabaseStatus> ProbeAsync(CancellationToken cancellationToken);
}
