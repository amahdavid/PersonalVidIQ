using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PersonalVidIQ.Application.DTOs.YouTube;
using PersonalVidIQ.Application.Interfaces;

namespace PersonalVidIQ.Infrastructure.Persistence;

/// <summary>Search cache shared by every process. Expired rows are ignored on read and deleted by the retention purge.</summary>
public sealed class PostgresSearchCache(IDbContextFactory<PersonalVidIQDbContext> contextFactory, TimeProvider timeProvider) : ISearchCache
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<SearchResult?> GetAsync(string key, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var json = await db.SearchCache.AsNoTracking()
            .Where(s => s.Key == key && s.ExpiresAtUtc > now)
            .Select(s => s.ResultJson)
            .SingleOrDefaultAsync(cancellationToken);

        return json is null ? null : JsonSerializer.Deserialize<SearchResult>(json, Json);
    }

    public async Task SetAsync(string key, SearchResult result, TimeSpan timeToLive, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var expires = now + timeToLive;
        var json = JsonSerializer.Serialize(result, Json);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO search_cache (key, result_json, created_at_utc, expires_at_utc)
            VALUES ({key}, {json}::jsonb, {now}, {expires})
            ON CONFLICT (key) DO UPDATE
            SET result_json = EXCLUDED.result_json,
                created_at_utc = EXCLUDED.created_at_utc,
                expires_at_utc = EXCLUDED.expires_at_utc
            """,
            cancellationToken);
    }
}
