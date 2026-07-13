using System.IO;
using System.Windows;
using ClipHistory.App.Clipboard;
using ClipHistory.App.Integration;
using ClipHistory.App.Localization;
using ClipHistory.Core.Models;
using ClipHistory.Infrastructure.Storage;
using ClipHistory.Infrastructure.Settings;
using Microsoft.Data.Sqlite;

namespace ClipHistory.App;

public partial class App : System.Windows.Application, IDisposable
{
    private SqliteConnection? connection;
    private TrayIconService? trayIconService;
    private SingleInstanceGuard? singleInstanceGuard;
    private bool disposed;

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        trayIconService?.Dispose();
        connection?.Dispose();
        singleInstanceGuard?.Dispose();
        GC.SuppressFinalize(this);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            LocalizationService.Apply(AppLanguage.FollowSystem);
            singleInstanceGuard = new SingleInstanceGuard();
            if (!singleInstanceGuard.IsPrimaryInstance)
            {
                System.Windows.MessageBox.Show(
                    "ClipHistory 已经在运行，请从系统托盘打开。",
                    "ClipHistory",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                Shutdown(0);
                return;
            }

            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            AppDataPaths paths = AppDataPaths.ForCurrentUser();
            SettingsFileStore settingsStore = new(paths);
            AppSettings settings = settingsStore.Load();
            LocalizationService.Apply(settings.Language);
            connection = new SqliteConnectionFactory(paths).OpenInitializedConnection();
            SqliteHistoryRepository repository = new(connection);
            ImageFileStore imageFileStore = new(paths);
            _ = new HistoryCleanupService(repository, imageFileStore).DeleteExpired(
                settings.RetentionPeriod,
                DateTimeOffset.UtcNow);
            ClipboardHistoryService clipboardService = new(repository, imageFileStore);

            MainWindow window = new(
                repository,
                imageFileStore,
                clipboardService,
                settingsStore,
                WindowsStartupService.ForCurrentProcess(),
                settings);
            MainWindow = window;
            trayIconService = new TrayIconService(window);
            window.Show();

            if (e.Args.Contains("--smoke-test", StringComparer.Ordinal))
            {
                Dispatcher.BeginInvoke(() =>
                {
                    window.RequestExit();
                    Shutdown(0);
                });
            }
        }
        catch (Exception exception)
        {
            WriteStartupError(exception);
            System.Windows.MessageBox.Show(
                $"ClipHistory 无法启动。\n\n{exception.Message}",
                "启动失败",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Dispose();
        base.OnExit(e);
    }

    private static void WriteStartupError(Exception exception)
    {
        try
        {
            AppDataPaths paths = AppDataPaths.ForCurrentUser();
            paths.EnsureDirectoriesExist();
            string logPath = Path.Combine(paths.LogsDirectory, "startup-error.log");
            File.AppendAllText(
                logPath,
                $"[{DateTimeOffset.UtcNow:O}] {exception}{Environment.NewLine}");
        }
        catch
        {
            // Startup reporting must never replace the original failure.
        }
    }
}
