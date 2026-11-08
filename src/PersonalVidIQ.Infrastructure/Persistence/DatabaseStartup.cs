using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PersonalVidIQ.Application.Interfaces;
using PersonalVidIQ.Application.Options;

namespace PersonalVidIQ.Infrastructure.Persistence;

public static class DatabaseStartup
{
    /// <summary>
    /// Applies pending migrations (when Database:AutoMigrate is on) and runs the retention purge.
    /// Never throws: a database problem is logged and reported by get_status instead of stopping the server.
    /// </summary>
    public static async Task PrepareAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("PersonalVidIQ.Startup");
        var databaseOptions = services.GetRequiredService<IOptions<DatabaseOptions>>().Value;

        try
        {
            if (!(await services.GetRequiredService<IDatabaseProbe>().ProbeAsync(cancellationToken)).Reachable)
            {
                logger.LogWarning("PostgreSQL is not reachable; skipping migrations and retention purge.");
                return;
            }

            var migrator = services.GetRequiredService<IDatabaseMigrator>();
            if (databaseOptions.AutoMigrate)
            {
                var applied = await migrator.MigrateAsync(cancellationToken);
                if (applied.Count > 0)
                {
                    logger.LogInformation("Applied migrations: {Migrations}", string.Join(", ", applied));
                }
            }
            else if ((await migrator.GetPendingMigrationsAsync(cancellationToken)).Count > 0)
            {
                logger.LogWarning("Database has pending migrations and AutoMigrate is off; skipping retention purge.");
                return;
            }

            await services.GetRequiredService<IRetentionService>().PurgeAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Database startup failed");
        }
    }
}
