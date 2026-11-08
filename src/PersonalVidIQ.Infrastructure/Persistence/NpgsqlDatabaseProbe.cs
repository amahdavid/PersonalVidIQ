using Microsoft.Extensions.Options;
using Npgsql;
using PersonalVidIQ.Application.DTOs;
using PersonalVidIQ.Application.Options;

namespace PersonalVidIQ.Infrastructure.Persistence;

public sealed class NpgsqlDatabaseProbe(IOptions<DatabaseOptions> options) : IDatabaseProbe
{
    public async Task<DatabaseStatus> ProbeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(options.Value.ConnectionString) { Timeout = 5 };
            await using var connection = new NpgsqlConnection(builder.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            return new DatabaseStatus(true, connection.PostgreSqlVersion.ToString(), null);
        }
        catch (Exception ex) when (ex is NpgsqlException or InvalidOperationException or ArgumentException or TimeoutException)
        {
            // Only the message: connection strings and credentials never reach the output.
            return new DatabaseStatus(false, null, ex.Message);
        }
    }
}
