using ClipHistory.Core.Models;

namespace ClipHistory.Core.Tests.Models;

public sealed class RetentionPeriodTests
{
    [Theory]
    [InlineData(RetentionPeriod.OneDay, 1)]
    [InlineData(RetentionPeriod.ThreeDays, 3)]
    [InlineData(RetentionPeriod.FiveDays, 5)]
    public void ToTimeSpanReturnsConfiguredNumberOfDays(
        RetentionPeriod retentionPeriod,
        int expectedDays)
    {
        Assert.Equal(TimeSpan.FromDays(expectedDays), retentionPeriod.ToTimeSpan());
    }

    [Fact]
    public void ToTimeSpanRejectsUnknownPeriod()
    {
        RetentionPeriod invalidPeriod = (RetentionPeriod)2;

        Assert.Throws<ArgumentOutOfRangeException>(() => invalidPeriod.ToTimeSpan());
    }
}
