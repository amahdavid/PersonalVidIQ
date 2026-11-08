using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PersonalVidIQ.Application.Interfaces;
using PersonalVidIQ.Application.Options;
using PersonalVidIQ.Infrastructure.Persistence;
using PersonalVidIQ.Infrastructure.Status;

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

        return services;
    }
}
