using ClipHistory.Core.Models;
using ClipHistory.Infrastructure.Settings;
using ClipHistory.Infrastructure.Storage;

namespace ClipHistory.Infrastructure.Tests.Settings;

public sealed class SettingsFileStoreTests
{
    [Fact]
    public void LoadReturnsDefaultsWhenFileDoesNotExist()
    {
        using TemporaryDirectory temporaryDirectory = new();
        SettingsFileStore store = new(new AppDataPaths(temporaryDirectory.Path));

        AppSettings settings = store.Load();

        Assert.Equal(AppSettings.Default, settings);
    }

    [Fact]
    public void SaveAndLoadRoundTripsSupportedSettings()
    {
        using TemporaryDirectory temporaryDirectory = new();
        AppDataPaths paths = new(temporaryDirectory.Path);
        SettingsFileStore store = new(paths);
        AppSettings expected = new(
            RetentionPeriod.FiveDays,
            AppLanguage.English,
            HotKeyOption.ControlAltV,
            StartWithWindows: true);

        store.Save(expected);
        AppSettings actual = store.Load();

        Assert.Equal(expected, actual);
        Assert.True(File.Exists(paths.SettingsPath));
        Assert.Empty(Directory.GetFiles(paths.RootDirectory, "*.tmp"));
    }

    [Fact]
    public void LoadReturnsDefaultsForMalformedOrUnsupportedSettings()
    {
        using TemporaryDirectory temporaryDirectory = new();
        AppDataPaths paths = new(temporaryDirectory.Path);
        paths.EnsureDirectoriesExist();
        SettingsFileStore store = new(paths);
        File.WriteAllText(paths.SettingsPath, "{ invalid json");

        Assert.Equal(AppSettings.Default, store.Load());

        File.WriteAllText(
            paths.SettingsPath,
            "{\"RetentionPeriod\":\"2\",\"Language\":\"FollowSystem\",\"StartWithWindows\":false}");
        Assert.Equal(AppSettings.Default, store.Load());
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            string tempRoot = System.IO.Path.GetFullPath(System.IO.Path.GetTempPath());
            Path = System.IO.Path.Combine(tempRoot, $"ClipHistory.Tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            string tempRoot = System.IO.Path.TrimEndingDirectorySeparator(
                System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()));
            string resolvedPath = System.IO.Path.GetFullPath(Path);
            if (!resolvedPath.StartsWith(
                tempRoot + System.IO.Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Refusing to delete a directory outside the temp root.");
            }

            if (Directory.Exists(resolvedPath))
            {
                Directory.Delete(resolvedPath, recursive: true);
            }
        }
    }
}
