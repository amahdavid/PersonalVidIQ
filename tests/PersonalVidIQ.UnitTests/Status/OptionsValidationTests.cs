using System.ComponentModel.DataAnnotations;
using PersonalVidIQ.Application.Options;

namespace PersonalVidIQ.UnitTests.Status;

public sealed class OptionsValidationTests
{
    private static bool IsValid(object options) =>
        Validator.TryValidateObject(options, new ValidationContext(options), null, validateAllProperties: true);

    [Fact]
    public void Defaults_are_valid()
    {
        Assert.True(IsValid(new YouTubeOptions()));
        Assert.True(IsValid(new DataCollectionOptions()));
    }

    [Fact]
    public void Retention_longer_than_30_days_is_rejected()
    {
        Assert.False(IsValid(new DataCollectionOptions { SnapshotRetentionDays = 31 }));
    }

    [Fact]
    public void Empty_connection_string_is_rejected()
    {
        Assert.False(IsValid(new DatabaseOptions()));
        Assert.True(IsValid(new DatabaseOptions { ConnectionString = "Host=localhost" }));
    }
}
