using System.Windows;
using System.Windows.Controls;
using ClipHistory.Core.Models;
using ClipHistory.Infrastructure.Settings;

namespace ClipHistory.App;

public partial class SettingsWindow : Window
{
    public SettingsWindow(AppSettings settings)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        InitializeComponent();
        SelectByTag(RetentionBox, ((int)settings.RetentionPeriod).ToString(System.Globalization.CultureInfo.InvariantCulture));
        SelectByTag(LanguageBox, settings.Language switch
        {
            AppLanguage.SimplifiedChinese => "Chinese",
            AppLanguage.English => "English",
            _ => "System",
        });
        SelectByTag(HotKeyBox, settings.HotKey.ToString());
        StartupCheckBox.IsChecked = settings.StartWithWindows;
    }

    public AppSettings Settings { get; private set; }

    public ClearHistoryMode? RequestedClear { get; private set; }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        string retentionTag = (RetentionBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag as string ?? "3";
        string languageTag = (LanguageBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag as string ?? "System";
        string hotKeyTag = (HotKeyBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag as string
            ?? nameof(HotKeyOption.ControlShiftV);
        Settings = new AppSettings(
            retentionTag switch
            {
                "1" => RetentionPeriod.OneDay,
                "5" => RetentionPeriod.FiveDays,
                _ => RetentionPeriod.ThreeDays,
            },
            languageTag switch
            {
                "Chinese" => AppLanguage.SimplifiedChinese,
                "English" => AppLanguage.English,
                _ => AppLanguage.FollowSystem,
            },
            Enum.Parse<HotKeyOption>(hotKeyTag),
            StartupCheckBox.IsChecked == true);
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void ClearRegularButton_Click(object sender, RoutedEventArgs e)
    {
        RequestClear(ClearHistoryMode.RegularOnly, "ConfirmClearRegular");
    }

    private void ClearAllButton_Click(object sender, RoutedEventArgs e)
    {
        RequestClear(ClearHistoryMode.All, "ConfirmClearAll");
    }

    private void RequestClear(ClearHistoryMode mode, string messageKey)
    {
        MessageBoxResult result = System.Windows.MessageBox.Show(
            Localization.LocalizationService.Get(messageKey),
            Localization.LocalizationService.Get("ConfirmTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (result == MessageBoxResult.Yes)
        {
            RequestedClear = mode;
            Close();
        }
    }

    private static void SelectByTag(System.Windows.Controls.ComboBox comboBox, string tag)
    {
        comboBox.SelectedItem = comboBox.Items
            .Cast<System.Windows.Controls.ComboBoxItem>()
            .First(item => string.Equals(item.Tag as string, tag, StringComparison.Ordinal));
    }
}
