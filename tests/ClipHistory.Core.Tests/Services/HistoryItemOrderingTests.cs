using ClipHistory.Core.Models;
using ClipHistory.Core.Services;

namespace ClipHistory.Core.Tests.Services;

public sealed class HistoryItemOrderingTests
{
    private static readonly DateTimeOffset BaseTime =
        new(2026, 7, 12, 4, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ForDisplayPlacesPinnedItemsFirstAndSortsEachGroupNewestFirst()
    {
        HistoryItem olderPinned = CreateItem(1, BaseTime.AddMinutes(1), isPinned: true);
        HistoryItem newerPinned = CreateItem(2, BaseTime.AddMinutes(3), isPinned: true);
        HistoryItem olderRegular = CreateItem(3, BaseTime, isPinned: false);
        HistoryItem newerRegular = CreateItem(4, BaseTime.AddMinutes(4), isPinned: false);

        IReadOnlyList<HistoryItem> ordered = HistoryItemOrdering.ForDisplay(
            [olderRegular, newerRegular, olderPinned, newerPinned]);

        Assert.Equal(
            [newerPinned, olderPinned, newerRegular, olderRegular],
            ordered);
    }

    [Fact]
    public void ForDisplayUsesIdAsDeterministicFinalTieBreaker()
    {
        HistoryItem higherId = CreateItem(2, BaseTime, isPinned: false);
        HistoryItem lowerId = CreateItem(1, BaseTime, isPinned: false);

        IReadOnlyList<HistoryItem> ordered =
            HistoryItemOrdering.ForDisplay([higherId, lowerId]);

        Assert.Equal([lowerId, higherId], ordered);
    }

    private static HistoryItem CreateItem(int idSuffix, DateTimeOffset copiedAtUtc, bool isPinned)
    {
        Guid id = new($"00000000-0000-0000-0000-{idSuffix:D12}");

        return new HistoryItem(
            id,
            ClipboardContentType.Text,
            $"hash-{idSuffix}",
            BaseTime,
            copiedAtUtc,
            BaseTime,
            isPinned,
            textContent: $"item-{idSuffix}");
    }
}
