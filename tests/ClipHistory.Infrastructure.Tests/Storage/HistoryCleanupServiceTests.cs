using ClipHistory.Core.Models;
using ClipHistory.Infrastructure.Storage;
using Microsoft.Data.Sqlite;

namespace ClipHistory.Infrastructure.Tests.Storage;

public sealed class HistoryCleanupServiceTests
{
    private static readonly DateTimeOffset BaseTime =
        new(2026, 7, 12, 4, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DeleteExpiredRemovesExpiredItemsAndStoredImagesButKeepsPinnedItems()
    {
        using TemporaryDirectory temporaryDirectory = new();
        AppDataPaths paths = new(temporaryDirectory.Path);
        ImageFileStore imageStore = new(paths);
        using SqliteConnection connection = CreateDatabase();
        SqliteHistoryRepository repository = new(connection);
        Guid imageId = Guid.NewGuid();
        string imagePath = imageStore.SavePng(imageId, [1, 2, 3]);
        HistoryItem expiredImage = new(
            imageId,
            ClipboardContentType.Image,
            "image-hash",
            BaseTime,
            BaseTime,
            BaseTime,
            imageRelativePath: imagePath);
        HistoryItem pinnedText = new(
            Guid.NewGuid(),
            ClipboardContentType.Text,
            "pinned-hash",
            BaseTime,
            BaseTime,
            BaseTime,
            isPinned: true,
            textContent: "keep");
        repository.Add(expiredImage);
        repository.Add(pinnedText);
        HistoryCleanupService cleanup = new(repository, imageStore);

        HistoryCleanupResult result = cleanup.DeleteExpired(
            RetentionPeriod.OneDay,
            BaseTime.AddDays(1));

        Assert.Equal(1, result.DeletedItemCount);
        Assert.Equal(1, result.DeletedImageCount);
        Assert.Equal(0, result.FailedImageDeleteCount);
        Assert.Null(repository.GetById(expiredImage.Id));
        Assert.NotNull(repository.GetById(pinnedText.Id));
        Assert.False(File.Exists(imageStore.GetAbsolutePath(imagePath)));
    }

    [Fact]
    public void DeleteExpiredReportsUnsafeOrphanImageWithoutTouchingOutsidePath()
    {
        using TemporaryDirectory temporaryDirectory = new();
        AppDataPaths paths = new(temporaryDirectory.Path);
        using SqliteConnection connection = CreateDatabase();
        SqliteHistoryRepository repository = new(connection);
        HistoryItem unsafeImage = new(
            Guid.NewGuid(),
            ClipboardContentType.Image,
            "unsafe-image-hash",
            BaseTime,
            BaseTime,
            BaseTime,
            imageRelativePath: "../outside.png");
        repository.Add(unsafeImage);
        HistoryCleanupService cleanup = new(repository, new ImageFileStore(paths));

        HistoryCleanupResult result = cleanup.DeleteExpired(
            RetentionPeriod.OneDay,
            BaseTime.AddDays(1));

        Assert.Equal(1, result.DeletedItemCount);
        Assert.Equal(0, result.DeletedImageCount);
        Assert.Equal(1, result.FailedImageDeleteCount);
    }

    private static SqliteConnection CreateDatabase()
    {
        SqliteConnection connection = new("Data Source=:memory:");
        connection.Open();
        SqliteDatabaseInitializer.Initialize(connection);
        return connection;
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
