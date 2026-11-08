using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PersonalVidIQ.Application.Options;
using PersonalVidIQ.Infrastructure.YouTube;

namespace PersonalVidIQ.UnitTests.YouTube;

public sealed class QuotaLedgerTests
{
    [Theory]
    // After the 1 Nov 2026 DST change Pacific is UTC-8, so the quota day starts at 08:00 UTC.
    [InlineData("2026-11-08T07:59:00Z", "2026-11-07T08:00:00Z")]
    [InlineData("2026-11-08T08:00:00Z", "2026-11-08T08:00:00Z")]
    // In summer Pacific is UTC-7.
    [InlineData("2026-07-01T06:59:00Z", "2026-06-30T07:00:00Z")]
    [InlineData("2026-07-01T07:00:00Z", "2026-07-01T07:00:00Z")]
    public void Quota_day_starts_at_midnight_pacific(string nowUtc, string expectedStartUtc) =>
        Assert.Equal(DateTimeOffset.Parse(expectedStartUtc), QuotaDay.StartUtc(DateTimeOffset.Parse(nowUtc)));

    [Fact]
    public void Next_start_crosses_dst_change()
    {
        // 1 Nov 2026 is the fall-back day: that Pacific day is 25 hours long.
        var start = QuotaDay.StartUtc(DateTimeOffset.Parse("2026-11-01T12:00:00Z"));
        Assert.Equal(DateTimeOffset.Parse("2026-11-01T07:00:00Z"), start);
        Assert.Equal(DateTimeOffset.Parse("2026-11-02T08:00:00Z"), QuotaDay.NextStartUtc(start));
    }

    [Fact]
    public void Records_units_and_calls_and_resets_on_new_day()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-11-08T12:00:00Z"));
        var ledger = new InMemoryQuotaLedger(Options.Create(new YouTubeOptions()), time);

        ledger.Record("search.list", 100);
        ledger.Record("videos.list", 1);
        ledger.Record("videos.list", 1);

        var usage = ledger.GetUsage();
        Assert.Equal(102, usage.UnitsUsed);
        Assert.Equal(9_898, usage.UnitsRemaining);
        Assert.Equal(1, usage.SearchCallsUsed);
        Assert.Equal(49, usage.SearchCallsRemaining);
        Assert.Equal(2, usage.CallsByEndpoint["videos.list"]);

        time.Advance(TimeSpan.FromHours(24));
        Assert.Equal(0, ledger.GetUsage().UnitsUsed);
    }
}
