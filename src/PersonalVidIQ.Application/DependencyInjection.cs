using Microsoft.Extensions.DependencyInjection;
using PersonalVidIQ.Application.Interfaces;
using PersonalVidIQ.Application.Research;
using PersonalVidIQ.Application.Services;

namespace PersonalVidIQ.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddPersonalVidIQApplication(this IServiceCollection services)
    {
        services.AddTransient<ChannelVideoLoader>();
        services.AddTransient<IVideoAnalysisService, VideoAnalysisService>();
        services.AddTransient<IChannelResearchService, ChannelResearchService>();
        services.AddTransient<IResearchService, ResearchService>();
        services.AddTransient<ITrackingService, TrackingService>();
        services.AddTransient<ICollectionService, CollectionService>();
        services.AddTransient<ResearchDataLoader>();
        services.AddTransient<IKeywordResearchService, KeywordResearchService>();
        services.AddTransient<ITrendResearchService, TrendResearchService>();
        return services;
    }
}
