using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ClipHistory.App.Clipboard;
using ClipHistory.App.Integration;
using ClipHistory.App.Localization;
using ClipHistory.Core.Models;
using ClipHistory.Infrastructure.Storage;
using ClipHistory.Infrastructure.Settings;

namespace ClipHistory.App;

public partial class MainWindow : Window, IDisposable
{
    private readonly SqliteHistoryRepository repository;
    private readonly ImageFileStore imageFileStore;
    private readonly ClipboardHistoryService clipboardService;
    private readonly SettingsFileStore settingsStore;
    private readonly WindowsStartupService startupService;
    private readonly ClipboardMonitor clipboardMonitor;
    private readonly GlobalHotKeyService globalHotKeyService;
    private readonly DispatcherTimer undoTimer;
    private readonly DispatcherTimer autoHideTimer;
    private readonly DispatcherTimer collapseGraceTimer;
    private HistoryItem? pendingUndo;
    private bool isPaused;
    private bool disposed;
    private bool exitRequested;
    private bool isModalDialogOpen;
    private AppSettings settings;

    public MainWindow(
        SqliteHistoryRepository repository,
        ImageFileStore imageFileStore,
        ClipboardHistoryService clipboardService,
        SettingsFileStore settingsStore,
        WindowsStartupService startupService,
        AppSettings settings)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.imageFileStore = imageFileStore ?? throw new ArgumentNullException(nameof(imageFileStore));
        this.clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        this.settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        this.startupService = startupService ?? throw new ArgumentNullException(nameof(startupService));
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        HistoryCards = [];
        DataContext = this;
        InitializeComponent();

        undoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        undoTimer.Tick += UndoTimer_Tick;
        autoHideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        autoHideTimer.Tick += AutoHideTimer_Tick;
        collapseGraceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        collapseGraceTimer.Tick += CollapseGraceTimer_Tick;
        clipboardMonitor = new ClipboardMonitor(this, OnClipboardChanged);
        globalHotKeyService = new GlobalHotKeyService(
            this,
            TogglePanelFromHotKey,
            () => StatusText.Text = LocalizationService.Get("HotKeyBusy"),
            settings.HotKey);
        Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;
        Loaded += MainWindow_Loaded;
        PositionTopCenter();
        RefreshHistory();
    }

    public ObservableCollection<HistoryCardViewModel> HistoryCards { get; }

    public bool IsPaused => isPaused;

    public event EventHandler? PauseStateChanged;

    public event EventHandler? HiddenToTray;

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        undoTimer.Stop();
        autoHideTimer.Stop();
        collapseGraceTimer.Stop();
        globalHotKeyService.Dispose();
        clipboardMonitor.Dispose();
        GC.SuppressFinalize(this);
    }

    public void ShowFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        ExpandPanel(startAutoHideCountdown: true);
        Activate();
        Focus();
    }

    public void TogglePanelFromHotKey()
    {
        if (isModalDialogOpen)
        {
            Activate();
            return;
        }

        if (IsVisible && RootPanel.Visibility == Visibility.Visible)
        {
            CollapsePanel();
            return;
        }

        ShowFromTray();
    }

    private void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        StartAutoHideCountdown();
    }

    private void PositionTopCenter()
    {
        Rect workArea = SystemParameters.WorkArea;
        Left = workArea.Left + (workArea.Width - Width) / 2;
        Top = workArea.Top;
    }

    private void ExpandPanel(bool startAutoHideCountdown)
    {
        collapseGraceTimer.Stop();
        HandleTab.Visibility = Visibility.Collapsed;
        RootPanel.Visibility = Visibility.Visible;
        if (startAutoHideCountdown)
        {
            StartAutoHideCountdown();
        }
        else
        {
            autoHideTimer.Stop();
        }
    }

    private void CollapsePanel()
    {
        autoHideTimer.Stop();
        collapseGraceTimer.Stop();
        RootPanel.Visibility = Visibility.Collapsed;
        HandleTab.Visibility = Visibility.Visible;
    }

    private void StartAutoHideCountdown()
    {
        autoHideTimer.Stop();
        autoHideTimer.Start();
    }

    private void RootGrid_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        collapseGraceTimer.Stop();
        autoHideTimer.Stop();
        if (RootPanel.Visibility != Visibility.Visible)
        {
            ExpandPanel(startAutoHideCountdown: false);
        }
    }

    private void RootGrid_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        collapseGraceTimer.Stop();
        collapseGraceTimer.Start();
    }

    private void CollapseGraceTimer_Tick(object? sender, EventArgs e)
    {
        collapseGraceTimer.Stop();
        if (isModalDialogOpen || Mouse.Captured is not null || FilterBox.IsDropDownOpen)
        {
            return;
        }

        CollapsePanel();
    }

    private void AutoHideTimer_Tick(object? sender, EventArgs e)
    {
        autoHideTimer.Stop();
        if (RootGrid.IsMouseOver || isModalDialogOpen || Mouse.Captured is not null || FilterBox.IsDropDownOpen)
        {
            return;
        }

        CollapsePanel();
    }

    private void TryCollapseIfMouseOutside()
    {
        if (isModalDialogOpen || RootGrid.IsMouseOver)
        {
            return;
        }

        CollapsePanel();
    }

    private void FilterBox_DropDownClosed(object sender, EventArgs e) => TryCollapseIfMouseOutside();

    private void SearchBox_ContextMenuClosing(object sender, ContextMenuEventArgs e) => TryCollapseIfMouseOutside();

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    public void TogglePause()
    {
        isPaused = !isPaused;
        PauseButton.Content = LocalizationService.Get(isPaused ? "Resume" : "Pause");
        StatusText.Text = LocalizationService.Get(isPaused ? "PausedStatus" : "Recording");
        PauseStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RequestExit()
    {
        exitRequested = true;
        Close();
    }

    private void OnClipboardChanged()
    {
        if (isPaused)
        {
            return;
        }

        try
        {
            if (clipboardService.TryCaptureCurrentClipboard(DateTimeOffset.UtcNow))
            {
                StatusText.Text = LocalizationService.Get("Captured");
                RefreshHistory();
            }
        }
        catch (ExternalException)
        {
            StatusText.Text = LocalizationService.Get("ClipboardBusy");
        }
        catch (Exception exception)
        {
            StatusText.Text = LocalizationService.Format("CaptureFailed", exception.Message);
        }
    }

    private void RefreshHistory()
    {
        string search = SearchBox?.Text ?? string.Empty;
        string filter = (FilterBox?.SelectedItem as ComboBoxItem)?.Tag as string ?? "All";
        IEnumerable<HistoryItem> filtered = repository.GetAll()
            .Where(item => MatchesFilter(item, filter))
            .Where(item => MatchesSearch(item, search));

        HistoryCards.Clear();
        foreach (HistoryItem item in filtered)
        {
            HistoryCards.Add(new HistoryCardViewModel(item, imageFileStore));
        }

        if (EmptyState is not null)
        {
            EmptyState.Visibility = HistoryCards.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        HistoryItem? item = FindItemFromButton(sender);
        if (item is null)
        {
            return;
        }

        if (item.ContentType == ClipboardContentType.Files
            && item.FilePaths.Any(path => !File.Exists(path) && !Directory.Exists(path)))
        {
            StatusText.Text = LocalizationService.Get("MissingCopy");
            return;
        }

        try
        {
            clipboardService.CopyToClipboard(item);
            StatusText.Text = LocalizationService.Get("Copied");
        }
        catch (Exception exception) when (exception is IOException or ExternalException)
        {
            StatusText.Text = LocalizationService.Format("CopyFailed", exception.Message);
        }
    }

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        HistoryItem? item = FindItemFromButton(sender);
        if (item is null)
        {
            return;
        }

        bool changed = item.IsPinned ? item.Unpin(DateTimeOffset.UtcNow) : item.Pin();
        if (changed)
        {
            repository.UpdateState(item);
            StatusText.Text = LocalizationService.Get(item.IsPinned ? "PinnedDone" : "UnpinnedDone");
            RefreshHistory();
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        HistoryItem? item = FindItemFromButton(sender);
        if (item is null)
        {
            return;
        }

        FinalizePendingDelete();
        if (!repository.Delete(item.Id))
        {
            StatusText.Text = LocalizationService.Get("AlreadyGone");
            RefreshHistory();
            return;
        }

        pendingUndo = item;
        UndoButton.Visibility = Visibility.Visible;
        undoTimer.Start();
        StatusText.Text = LocalizationService.Get("DeletedUndo");
        RefreshHistory();
    }

    private void UndoButton_Click(object sender, RoutedEventArgs e)
    {
        if (pendingUndo is null)
        {
            return;
        }

        repository.Add(pendingUndo);
        pendingUndo = null;
        undoTimer.Stop();
        UndoButton.Visibility = Visibility.Collapsed;
        StatusText.Text = LocalizationService.Get("Restored");
        RefreshHistory();
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        TogglePause();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        isModalDialogOpen = true;
        try
        {
            SettingsWindow window = new(settings) { Owner = this };
            bool? result = window.ShowDialog();
            if (window.RequestedClear is not null)
            {
                ClearHistory(window.RequestedClear.Value);
                return;
            }

            if (result != true)
            {
                return;
            }

            try
            {
                HotKeyOption previousHotKey = settings.HotKey;
                if (!globalHotKeyService.TryChange(window.Settings.HotKey))
                {
                    StatusText.Text = LocalizationService.Get("HotKeyBusy");
                    return;
                }

                startupService.SetEnabled(window.Settings.StartWithWindows);
                try
                {
                    settingsStore.Save(window.Settings);
                    settings = window.Settings;
                }
                catch
                {
                    _ = globalHotKeyService.TryChange(previousHotKey);
                    throw;
                }
                LocalizationService.Apply(settings.Language);
                HistoryCleanupResult cleanup = new HistoryCleanupService(repository, imageFileStore)
                    .DeleteExpired(settings.RetentionPeriod, DateTimeOffset.UtcNow);
                StatusText.Text = cleanup.DeletedItemCount == 0
                    ? LocalizationService.Get("SettingsSaved")
                    : LocalizationService.Format("SettingsCleaned", cleanup.DeletedItemCount);
                RefreshHistory();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                StatusText.Text = LocalizationService.Format("SettingsFailed", exception.Message);
            }
        }
        finally
        {
            isModalDialogOpen = false;
            TryCollapseIfMouseOutside();
        }
    }

    private void ClearHistory(ClearHistoryMode mode)
    {
        FinalizePendingDelete();
        HistoryItem[] items = repository.GetAll()
            .Where(item => mode == ClearHistoryMode.All || !item.IsPinned)
            .ToArray();
        int deleted = repository.DeleteMany(items.Select(item => item.Id));

        foreach (HistoryItem item in items.Where(item =>
                     item.ContentType == ClipboardContentType.Image
                     && item.ImageRelativePath is not null))
        {
            try
            {
                imageFileStore.Delete(item.ImageRelativePath!);
            }
            catch (IOException)
            {
                // The record is gone; a later maintenance pass can remove an orphaned image.
            }
            catch (UnauthorizedAccessException)
            {
                // The record is gone; a later maintenance pass can remove an orphaned image.
            }
        }

        StatusText.Text = LocalizationService.Format("ClearedCount", deleted);
        RefreshHistory();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshHistory();

    private void FilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshHistory();

    private void UndoTimer_Tick(object? sender, EventArgs e) => FinalizePendingDelete();

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (exitRequested)
        {
            return;
        }

        e.Cancel = true;
        Hide();
        HiddenToTray?.Invoke(this, EventArgs.Empty);
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        Dispose();
        FinalizePendingDelete();
    }

    private void FinalizePendingDelete()
    {
        undoTimer.Stop();
        if (pendingUndo?.ContentType == ClipboardContentType.Image
            && pendingUndo.ImageRelativePath is not null)
        {
            try
            {
                imageFileStore.Delete(pendingUndo.ImageRelativePath);
            }
            catch (IOException)
            {
                StatusText.Text = "记录已删除，但图片文件稍后再清理";
            }
            catch (UnauthorizedAccessException)
            {
                StatusText.Text = "记录已删除，但图片文件稍后再清理";
            }
        }

        pendingUndo = null;
        if (UndoButton is not null)
        {
            UndoButton.Visibility = Visibility.Collapsed;
        }
    }

    private HistoryItem? FindItemFromButton(object sender)
    {
        return sender is System.Windows.Controls.Button { Tag: Guid id }
            ? repository.GetById(id)
            : null;
    }

    private static bool MatchesFilter(HistoryItem item, string filter)
    {
        return filter switch
        {
            "Text" => item.ContentType == ClipboardContentType.Text,
            "Image" => item.ContentType == ClipboardContentType.Image,
            "Files" => item.ContentType == ClipboardContentType.Files,
            "Pinned" => item.IsPinned,
            _ => true,
        };
    }

    private static bool MatchesSearch(HistoryItem item, string search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        return item.TextContent?.Contains(search, StringComparison.CurrentCultureIgnoreCase) == true
            || item.FilePaths.Any(path => path.Contains(search, StringComparison.CurrentCultureIgnoreCase));
    }
}
