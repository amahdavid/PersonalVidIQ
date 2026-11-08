using Microsoft.Extensions.DependencyInjection;
using PersonalVidIQ.Application.Interfaces;
using PersonalVidIQ.Application.Services;

namespace PersonalVidIQ.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddPersonalVidIQApplication(this IServiceCollection services)
    {
        services.AddTransient<ChannelVideoLoader>();
        services.AddTransient<IVideoAnalysisService, VideoAnalysisService>();
        services.AddTransient<IChannelResearchService, ChannelResearchService>();
        return services;
    }
}
