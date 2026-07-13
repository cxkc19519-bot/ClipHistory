namespace ClipHistory.Core.Models;

public static class RetentionPeriodExtensions
{
    public static TimeSpan ToTimeSpan(this RetentionPeriod retentionPeriod)
    {
        return retentionPeriod switch
        {
            RetentionPeriod.OneDay => TimeSpan.FromDays(1),
            RetentionPeriod.ThreeDays => TimeSpan.FromDays(3),
            RetentionPeriod.FiveDays => TimeSpan.FromDays(5),
            _ => throw new ArgumentOutOfRangeException(
                nameof(retentionPeriod),
                retentionPeriod,
                "The retention period must be 1, 3, or 5 days."),
        };
    }
}

