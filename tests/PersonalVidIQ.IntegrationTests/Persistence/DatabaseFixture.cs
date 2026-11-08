using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using PersonalVidIQ.Infrastructure.Persistence;

[assembly: AssemblyFixture(typeof(PersonalVidIQ.IntegrationTests.Persistence.DatabaseFixture))]

namespace PersonalVidIQ.IntegrationTests.Persistence;

/// <summary>Rebuilds the test database from the migrations once per run; tests reset data with <see cref="ResetAsync"/>.</summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    public IDbContextFactory<PersonalVidIQDbContext> Factory { get; } = CreateFactory();

    private static PooledDbContextFactory<PersonalVidIQDbContext> CreateFactory()
    {
        var options = new DbContextOptionsBuilder<PersonalVidIQDbContext>();
        DbContextConfiguration.Configure(options, TestSettings.ConnectionString);
        return new PooledDbContextFactory<PersonalVidIQDbContext>(options.Options);
    }

    public async ValueTask InitializeAsync()
    {
        await using var db = await Factory.CreateDbContextAsync();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
    }

    public async Task ResetAsync()
    {
        await using var db = await Factory.CreateDbContextAsync();
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE video_snapshots, channel_snapshots, videos, channels, quota_usage, search_cache, collection_runs RESTART IDENTITY CASCADE");
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
