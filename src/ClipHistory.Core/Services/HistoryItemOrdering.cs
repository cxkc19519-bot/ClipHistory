using ClipHistory.Core.Models;

namespace ClipHistory.Core.Services;

public static class HistoryItemOrdering
{
    public static IReadOnlyList<HistoryItem> ForDisplay(IEnumerable<HistoryItem> historyItems)
    {
        ArgumentNullException.ThrowIfNull(historyItems);

        return historyItems
            .OrderByDescending(item => item.IsPinned)
            .ThenByDescending(item => item.LastCopiedAtUtc)
            .ThenByDescending(item => item.CreatedAtUtc)
            .ThenBy(item => item.Id)
            .ToArray();
    }
}
