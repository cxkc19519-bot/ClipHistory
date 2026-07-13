using ClipHistory.Core.Models;

namespace ClipHistory.Infrastructure.Settings;

public sealed record AppSettings(
    RetentionPeriod RetentionPeriod,
    AppLanguage Language,
    HotKeyOption HotKey,
    bool StartWithWindows)
{
    public static AppSettings Default { get; } = new(
        RetentionPeriod.ThreeDays,
        AppLanguage.FollowSystem,
        HotKeyOption.ControlShiftV,
        StartWithWindows: false);
}
