using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PersonalVidIQ.Application.DTOs.YouTube;
using PersonalVidIQ.Application.Options;
using PersonalVidIQ.Infrastructure.Persistence;

namespace PersonalVidIQ.IntegrationTests.Persistence;

public sealed class QuotaAndCacheTests(DatabaseFixture db) : IAsyncLifetime
{
    private readonly FakeTimeProvider _time = new(TestData.Now);

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await db.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private PostgresQuotaLedger Ledger() => new(db.Factory, Options.Create(new YouTubeOptions()), _time);

    [Fact]
    public async Task Quota_is_shared_between_ledger_instances_and_survives_restarts()
    {
        await Ledger().RecordAsync("search.list", 100, Ct);
        await Ledger().RecordAsync("videos.list", 1, Ct);
        await Ledger().RecordAsync("videos.list", 1, Ct);

        var usage = await Ledger().GetUsageAsync(Ct);
        Assert.Equal(102, usage.UnitsUsed);
        Assert.Equal(1, usage.SearchCallsUsed);
        Assert.Equal(2, usage.CallsByEndpoint["videos.list"]);
    }

    [Fact]
    public async Task Concurrent_records_are_not_lost()
    {
        await Task.WhenAll(Enumerable.Range(0, 40).Select(_ => Ledger().RecordAsync("videos.list", 1, Ct)));

        Assert.Equal(40, (await Ledger().GetUsageAsync(Ct)).UnitsUsed);
    }

    [Fact]
    public async Task Quota_resets_on_the_next_pacific_day()
    {
        await Ledger().RecordAsync("search.list", 100, Ct);
        _time.Advance(TimeSpan.FromDays(1));

        Assert.Equal(0, (await Ledger().GetUsageAsync(Ct)).UnitsUsed);
    }

    [Fact]
    public async Task Search_cache_round_trips_and_expires()
    {
        var cache = new PostgresSearchCache(db.Factory, _time);
        var request = new SearchRequest("messi stats", 10, SearchOrder.ViewCount, TestData.Now.AddDays(-7), "GB", SearchDuration.Short);
        var hit = new SearchHit("aaaaaaaaaaa", "UCaaaaaaaaaaaaaaaaaaaaaa", "Football Facts", "Messi's stat & more", "", TestData.Now, null);
        await cache.SetAsync("k", new SearchResult(request, [hit], 1000, false, TestData.Now), TimeSpan.FromHours(24), Ct);

        var cached = await cache.GetAsync("k", Ct);
        Assert.NotNull(cached);
        Assert.Equal(request, cached.Request);
        Assert.Equal("Messi's stat & more", Assert.Single(cached.Hits).Title);

        // Overwrite keeps one row.
        await cache.SetAsync("k", cached with { TotalResultsEstimate = 5 }, TimeSpan.FromHours(24), Ct);
        Assert.Equal(5, (await cache.GetAsync("k", Ct))!.TotalResultsEstimate);

        _time.Advance(TimeSpan.FromHours(25));
        Assert.Null(await cache.GetAsync("k", Ct));
    }

    [Fact]
    public async Task Model_has_no_changes_missing_from_migrations()
    {
        await using var ctx = await db.Factory.CreateDbContextAsync(Ct);
        Assert.False(ctx.Database.HasPendingModelChanges(), "Run: dotnet ef migrations add <Name> --project src/PersonalVidIQ.Infrastructure --output-dir Persistence/Migrations");
    }
}
