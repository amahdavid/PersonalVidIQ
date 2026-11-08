using Microsoft.EntityFrameworkCore;

namespace PersonalVidIQ.Infrastructure.Persistence;

public interface IDatabaseMigrator
{
    Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> MigrateAsync(CancellationToken cancellationToken);
}

public sealed class DatabaseMigrator(IDbContextFactory<PersonalVidIQDbContext> contextFactory) : IDatabaseMigrator
{
    public async Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
    }

    public async Task<IReadOnlyList<string>> MigrateAsync(CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count > 0)
        {
            await db.Database.MigrateAsync(cancellationToken);
        }

        return pending;
    }
}
