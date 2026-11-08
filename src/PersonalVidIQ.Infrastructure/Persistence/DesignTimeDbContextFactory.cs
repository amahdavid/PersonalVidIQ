using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PersonalVidIQ.Infrastructure.Persistence;

/// <summary>Used only by `dotnet ef` to create migrations. No secrets: local trust auth.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PersonalVidIQDbContext>
{
    public PersonalVidIQDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("Database__ConnectionString")
            ?? "Host=localhost;Port=5432;Database=personalvidiq";
        var options = new DbContextOptionsBuilder<PersonalVidIQDbContext>();
        DbContextConfiguration.Configure(options, connectionString);
        return new PersonalVidIQDbContext(options.Options);
    }
}

internal static class DbContextConfiguration
{
    public static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention();
}
