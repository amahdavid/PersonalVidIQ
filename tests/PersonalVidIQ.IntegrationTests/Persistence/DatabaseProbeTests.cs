using Microsoft.Extensions.Options;
using PersonalVidIQ.Application.Options;
using PersonalVidIQ.Infrastructure.Persistence;

namespace PersonalVidIQ.IntegrationTests.Persistence;

public sealed class DatabaseProbeTests
{
    [Fact]
    public async Task Local_postgres_is_reachable()
    {
        var probe = new NpgsqlDatabaseProbe(Options.Create(new DatabaseOptions { ConnectionString = TestSettings.ConnectionString }));

        var status = await probe.ProbeAsync(TestContext.Current.CancellationToken);

        Assert.True(status.Reachable, status.Error);
        Assert.NotNull(status.ServerVersion);
    }

    [Fact]
    public async Task Unreachable_server_reports_error_without_throwing()
    {
        var probe = new NpgsqlDatabaseProbe(Options.Create(new DatabaseOptions { ConnectionString = "Host=localhost;Port=1;Database=nope" }));

        var status = await probe.ProbeAsync(TestContext.Current.CancellationToken);

        Assert.False(status.Reachable);
        Assert.False(string.IsNullOrWhiteSpace(status.Error));
    }
}
