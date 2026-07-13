namespace ClipHistory.Infrastructure.Storage;

public sealed class AppDataPaths
{
    public AppDataPaths(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        RootDirectory = Path.GetFullPath(rootDirectory);
        DataDirectory = Path.Combine(RootDirectory, "data");
        ImagesDirectory = Path.Combine(RootDirectory, "images");
        LogsDirectory = Path.Combine(RootDirectory, "logs");
        DatabasePath = Path.Combine(DataDirectory, "history.db");
        SettingsPath = Path.Combine(RootDirectory, "settings.json");
    }

    public string RootDirectory { get; }

    public string DataDirectory { get; }

    public string ImagesDirectory { get; }

    public string LogsDirectory { get; }

    public string DatabasePath { get; }

    public string SettingsPath { get; }

    public static AppDataPaths ForCurrentUser()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            throw new InvalidOperationException("The local application data directory is unavailable.");
        }

        return new AppDataPaths(Path.Combine(localAppData, "ClipHistory"));
    }

    public void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(ImagesDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }
}

