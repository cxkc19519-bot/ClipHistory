using System.Drawing;
using System.Windows.Forms;
using ClipHistory.App.Localization;

namespace ClipHistory.App.Integration;

public sealed class TrayIconService : IDisposable
{
    private readonly MainWindow window;
    private readonly NotifyIcon notifyIcon;
    private readonly ToolStripMenuItem pauseMenuItem;
    private readonly Icon? applicationIcon;
    private bool disposed;

    public TrayIconService(MainWindow window)
    {
        this.window = window ?? throw new ArgumentNullException(nameof(window));

        ToolStripMenuItem openMenuItem = new(LocalizationService.Get("TrayOpen"));
        openMenuItem.Click += OpenMenuItem_Click;
        pauseMenuItem = new ToolStripMenuItem(LocalizationService.Get("Pause"));
        pauseMenuItem.Click += PauseMenuItem_Click;
        ToolStripMenuItem exitMenuItem = new(LocalizationService.Get("Exit"));
        exitMenuItem.Click += ExitMenuItem_Click;

        ContextMenuStrip menu = new();
        menu.Items.Add(openMenuItem);
        menu.Items.Add(pauseMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitMenuItem);

        applicationIcon = LoadApplicationIcon();
        notifyIcon = new NotifyIcon
        {
            Icon = applicationIcon ?? SystemIcons.Application,
            Text = "ClipHistory 历史剪贴板",
            ContextMenuStrip = menu,
            Visible = true,
        };
        notifyIcon.DoubleClick += NotifyIcon_DoubleClick;
        window.PauseStateChanged += Window_PauseStateChanged;
        window.HiddenToTray += Window_HiddenToTray;
        LocalizationService.LanguageChanged += LocalizationService_LanguageChanged;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        window.PauseStateChanged -= Window_PauseStateChanged;
        window.HiddenToTray -= Window_HiddenToTray;
        LocalizationService.LanguageChanged -= LocalizationService_LanguageChanged;
        notifyIcon.DoubleClick -= NotifyIcon_DoubleClick;
        notifyIcon.Visible = false;
        notifyIcon.ContextMenuStrip?.Dispose();
        notifyIcon.Dispose();
        applicationIcon?.Dispose();
        GC.SuppressFinalize(this);
    }

    private static Icon? LoadApplicationIcon()
    {
        try
        {
            string? executablePath = Environment.ProcessPath;
            return string.IsNullOrWhiteSpace(executablePath)
                ? null
                : Icon.ExtractAssociatedIcon(executablePath);
        }
        catch
        {
            // A tray icon must not prevent the application from starting.
            return null;
        }
    }

    private void OpenMenuItem_Click(object? sender, EventArgs e) => window.ShowFromTray();

    private void PauseMenuItem_Click(object? sender, EventArgs e) => window.TogglePause();

    private void ExitMenuItem_Click(object? sender, EventArgs e)
    {
        window.RequestExit();
        System.Windows.Application.Current.Shutdown();
    }

    private void NotifyIcon_DoubleClick(object? sender, EventArgs e) => window.ShowFromTray();

    private void Window_PauseStateChanged(object? sender, EventArgs e)
    {
        pauseMenuItem.Text = window.IsPaused ? LocalizationService.Get("Resume") : LocalizationService.Get("Pause");
    }

    private void Window_HiddenToTray(object? sender, EventArgs e)
    {
        notifyIcon.ShowBalloonTip(
            2500,
            LocalizationService.Get("TrayTitle"),
            LocalizationService.Get("TrayMessage"),
            ToolTipIcon.Info);
    }

    private void LocalizationService_LanguageChanged(object? sender, EventArgs e)
    {
        if (notifyIcon.ContextMenuStrip?.Items.Count >= 4)
        {
            notifyIcon.ContextMenuStrip.Items[0].Text = LocalizationService.Get("TrayOpen");
            pauseMenuItem.Text = window.IsPaused ? LocalizationService.Get("Resume") : LocalizationService.Get("Pause");
            notifyIcon.ContextMenuStrip.Items[3].Text = LocalizationService.Get("Exit");
        }
    }
}
