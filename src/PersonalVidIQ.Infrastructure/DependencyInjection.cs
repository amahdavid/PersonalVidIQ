using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PersonalVidIQ.Application.Interfaces;
using PersonalVidIQ.Application.Options;
using PersonalVidIQ.Infrastructure.Persistence;
using PersonalVidIQ.Infrastructure.Status;
using PersonalVidIQ.Infrastructure.YouTube;

namespace PersonalVidIQ.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPersonalVidIQInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<YouTubeOptions>()
            .Bind(configuration.GetSection(YouTubeOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<DataCollectionOptions>()
            .Bind(configuration.GetSection(DataCollectionOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IDatabaseProbe, NpgsqlDatabaseProbe>();
        services.AddSingleton<ISystemStatusService, SystemStatusService>();

        services.AddPooledDbContextFactory<PersonalVidIQDbContext>((sp, options) =>
            DbContextConfiguration.Configure(options, sp.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString));
        services.AddSingleton<IDatabaseMigrator, DatabaseMigrator>();
        services.AddSingleton<IStorageProbe, StorageProbe>();
        services.AddSingleton<IChannelStore, ChannelStore>();
        services.AddSingleton<IVideoStore, VideoStore>();
        services.AddSingleton<ISnapshotStore, SnapshotStore>();
        services.AddSingleton<IRetentionService, RetentionService>();
        services.AddSingleton<IQuotaLedger, PostgresQuotaLedger>();
        services.AddSingleton<ISearchCache, PostgresSearchCache>();
        services.AddHttpClient<IYouTubeDataClient, YouTubeDataClient>((sp, client) =>
        {
            var yt = sp.GetRequiredService<IOptions<YouTubeOptions>>().Value;
            client.BaseAddress = new Uri(yt.BaseUrl.EndsWith('/') ? yt.BaseUrl : yt.BaseUrl + "/");
            client.Timeout = TimeSpan.FromSeconds(yt.RequestTimeoutSeconds);
        });

        return services;
    }
}
