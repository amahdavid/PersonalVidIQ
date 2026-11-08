using PersonalVidIQ.Application.DTOs.YouTube;

namespace PersonalVidIQ.Application.Interfaces;

/// <summary>Stores search.list results for the search cache window so repeat queries cost 0 units.</summary>
public interface ISearchCache
{
    Task<SearchResult?> GetAsync(string key, CancellationToken cancellationToken);

    Task SetAsync(string key, SearchResult result, TimeSpan timeToLive, CancellationToken cancellationToken);
}
