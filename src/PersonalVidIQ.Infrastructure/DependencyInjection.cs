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

        services.AddMemoryCache();
        services.AddSingleton<IQuotaLedger, InMemoryQuotaLedger>();
        services.AddHttpClient<IYouTubeDataClient, YouTubeDataClient>((sp, client) =>
        {
            var yt = sp.GetRequiredService<IOptions<YouTubeOptions>>().Value;
            client.BaseAddress = new Uri(yt.BaseUrl.EndsWith('/') ? yt.BaseUrl : yt.BaseUrl + "/");
            client.Timeout = TimeSpan.FromSeconds(yt.RequestTimeoutSeconds);
        });

        return services;
    }
}
