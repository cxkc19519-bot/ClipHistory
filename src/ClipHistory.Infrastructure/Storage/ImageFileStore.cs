namespace ClipHistory.Infrastructure.Storage;

public sealed class ImageFileStore
{
    private readonly AppDataPaths paths;

    public ImageFileStore(AppDataPaths paths)
    {
        this.paths = paths ?? throw new ArgumentNullException(nameof(paths));
    }

    public string SavePng(Guid historyItemId, byte[] pngBytes)
    {
        if (historyItemId == Guid.Empty)
        {
            throw new ArgumentException("The history item ID cannot be empty.", nameof(historyItemId));
        }

        ArgumentNullException.ThrowIfNull(pngBytes);
        if (pngBytes.Length == 0)
        {
            throw new ArgumentException("PNG data cannot be empty.", nameof(pngBytes));
        }

        paths.EnsureDirectoriesExist();
        string fileName = $"{historyItemId:N}.png";
        string destinationPath = Path.Combine(paths.ImagesDirectory, fileName);
        string temporaryPath = Path.Combine(paths.ImagesDirectory, $".{fileName}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllBytes(temporaryPath, pngBytes);
            File.Move(temporaryPath, destinationPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }

        return Path.GetRelativePath(paths.RootDirectory, destinationPath);
    }

    public string GetAbsolutePath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException("Image paths must be relative to the application data root.", nameof(relativePath));
        }

        string imagesRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(paths.ImagesDirectory));
        string resolvedPath = Path.GetFullPath(Path.Combine(paths.RootDirectory, relativePath));
        string requiredPrefix = imagesRoot + Path.DirectorySeparatorChar;

        if (!resolvedPath.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The image path is outside the application image directory.", nameof(relativePath));
        }

        return resolvedPath;
    }

    public bool Delete(string relativePath)
    {
        string absolutePath = GetAbsolutePath(relativePath);
        if (!File.Exists(absolutePath))
        {
            return false;
        }

        File.Delete(absolutePath);
        return true;
    }
}

