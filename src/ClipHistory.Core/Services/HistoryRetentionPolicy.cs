using ClipHistory.Core.Models;

namespace ClipHistory.Core.Services;

public static class HistoryRetentionPolicy
{
    public static bool IsExpired(
        HistoryItem historyItem,
        RetentionPeriod retentionPeriod,
        DateTimeOffset asOfUtc)
    {
        ArgumentNullException.ThrowIfNull(historyItem);

        if (asOfUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The expiration check time must use the UTC offset.", nameof(asOfUtc));
        }

        TimeSpan retentionDuration = retentionPeriod.ToTimeSpan();

        if (historyItem.IsPinned)
        {
            return false;
        }

        DateTimeOffset expiresAtUtc = historyItem.RetentionBaseAtUtc.Add(retentionDuration);
        return asOfUtc >= expiresAtUtc;
    }
}

