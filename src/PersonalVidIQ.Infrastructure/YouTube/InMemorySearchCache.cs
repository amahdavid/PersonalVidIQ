using Microsoft.Extensions.Caching.Memory;
using PersonalVidIQ.Application.DTOs.YouTube;
using PersonalVidIQ.Application.Interfaces;

namespace PersonalVidIQ.Infrastructure.YouTube;

/// <summary>Per-process search cache for tests. The app uses <see cref="PostgresSearchCache"/>.</summary>
public sealed class InMemorySearchCache(IMemoryCache cache) : ISearchCache
{
    public Task<SearchResult?> GetAsync(string key, CancellationToken cancellationToken) =>
        Task.FromResult(cache.TryGetValue(key, out SearchResult? result) ? result : null);

    public Task SetAsync(string key, SearchResult result, TimeSpan timeToLive, CancellationToken cancellationToken)
    {
        cache.Set(key, result, timeToLive);
        return Task.CompletedTask;
    }
}
