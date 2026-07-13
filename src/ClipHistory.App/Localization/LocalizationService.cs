using System.Globalization;
using System.Windows;
using ClipHistory.Infrastructure.Settings;

namespace ClipHistory.App.Localization;

public static class LocalizationService
{
    private static ResourceDictionary? currentDictionary;

    public static event EventHandler? LanguageChanged;

    public static void Apply(AppLanguage language)
    {
        AppLanguage resolved = language == AppLanguage.FollowSystem
            ? ResolveSystemLanguage()
            : language;
        string resourceName = resolved == AppLanguage.SimplifiedChinese
            ? "Strings.zh-CN.xaml"
            : "Strings.en-US.xaml";
        ResourceDictionary dictionary = new()
        {
            Source = new Uri(
                $"pack://application:,,,/ClipHistory.App;component/Localization/{resourceName}",
                UriKind.Absolute),
        };

        if (currentDictionary is not null)
        {
            System.Windows.Application.Current.Resources.MergedDictionaries.Remove(currentDictionary);
        }

        currentDictionary = dictionary;
        System.Windows.Application.Current.Resources.MergedDictionaries.Add(dictionary);
        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    public static string Get(string key)
    {
        return System.Windows.Application.Current.TryFindResource(key) as string ?? key;
    }

    public static string Format(string key, params object?[] arguments)
    {
        return string.Format(CultureInfo.CurrentCulture, Get(key), arguments);
    }

    private static AppLanguage ResolveSystemLanguage()
    {
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals(
            "zh",
            StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.SimplifiedChinese
            : AppLanguage.English;
    }
}
