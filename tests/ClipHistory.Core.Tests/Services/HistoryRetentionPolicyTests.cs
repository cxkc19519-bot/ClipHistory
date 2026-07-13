using ClipHistory.Core.Models;
using ClipHistory.Core.Services;

namespace ClipHistory.Core.Tests.Services;

public sealed class HistoryRetentionPolicyTests
{
    private static readonly DateTimeOffset RetentionBaseAtUtc =
        new(2026, 7, 12, 4, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(RetentionPeriod.OneDay, 1)]
    [InlineData(RetentionPeriod.ThreeDays, 3)]
    [InlineData(RetentionPeriod.FiveDays, 5)]
    public void ItemExpiresAtExactRetentionBoundary(
        RetentionPeriod retentionPeriod,
        int retentionDays)
    {
        HistoryItem item = CreateItem(isPinned: false);
        DateTimeOffset exactBoundary = RetentionBaseAtUtc.AddDays(retentionDays);

        Assert.False(HistoryRetentionPolicy.IsExpired(
            item,
            retentionPeriod,
            exactBoundary.AddTicks(-1)));
        Assert.True(HistoryRetentionPolicy.IsExpired(item, retentionPeriod, exactBoundary));
        Assert.True(HistoryRetentionPolicy.IsExpired(
            item,
            retentionPeriod,
            exactBoundary.AddTicks(1)));
    }

    [Fact]
    public void PinnedItemNeverExpires()
    {
        HistoryItem item = CreateItem(isPinned: true);
        DateTimeOffset farFuture = RetentionBaseAtUtc.AddYears(50);

        bool isExpired = HistoryRetentionPolicy.IsExpired(
            item,
            RetentionPeriod.OneDay,
            farFuture);

        Assert.False(isExpired);
    }

    [Fact]
    public void UnpinnedItemExpiresFromNewRetentionBase()
    {
        HistoryItem item = CreateItem(isPinned: true);
        DateTimeOffset unpinnedAtUtc = RetentionBaseAtUtc.AddDays(10);
        item.Unpin(unpinnedAtUtc);

        Assert.False(HistoryRetentionPolicy.IsExpired(
            item,
            RetentionPeriod.ThreeDays,
            unpinnedAtUtc.AddDays(3).AddTicks(-1)));
        Assert.True(HistoryRetentionPolicy.IsExpired(
            item,
            RetentionPeriod.ThreeDays,
            unpinnedAtUtc.AddDays(3)));
    }

    [Fact]
    public void ExpirationCheckRejectsNonUtcTime()
    {
        HistoryItem item = CreateItem(isPinned: false);
        DateTimeOffset nonUtcTime = RetentionBaseAtUtc.ToOffset(TimeSpan.FromHours(8));

        Assert.Throws<ArgumentException>(() => HistoryRetentionPolicy.IsExpired(
            item,
            RetentionPeriod.OneDay,
            nonUtcTime));
    }

    [Fact]
    public void ExpirationCheckRejectsUnknownRetentionPeriod()
    {
        HistoryItem item = CreateItem(isPinned: true);
        RetentionPeriod unknownPeriod = (RetentionPeriod)2;

        Assert.Throws<ArgumentOutOfRangeException>(() => HistoryRetentionPolicy.IsExpired(
            item,
            unknownPeriod,
            RetentionBaseAtUtc));
    }

    private static HistoryItem CreateItem(bool isPinned)
    {
        return new HistoryItem(
            Guid.NewGuid(),
            ClipboardContentType.Text,
            "text-hash",
            RetentionBaseAtUtc,
            RetentionBaseAtUtc,
            RetentionBaseAtUtc,
            isPinned,
            textContent: "hello");
    }
}
