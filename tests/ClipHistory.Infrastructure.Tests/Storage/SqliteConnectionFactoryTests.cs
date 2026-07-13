using ClipHistory.Core.Models;
using ClipHistory.Infrastructure.Storage;
using Microsoft.Data.Sqlite;

namespace ClipHistory.Infrastructure.Tests.Storage;

public sealed class SqliteConnectionFactoryTests
{
    private static readonly DateTimeOffset Timestamp =
        new(2026, 7, 12, 4, 0, 0, TimeSpan.Zero);

    [Fact]
    public void OpenInitializedConnectionCreatesExpectedApplicationDirectories()
    {
        using TemporaryDirectory temporaryDirectory = new();
        AppDataPaths paths = new(temporaryDirectory.Path);
        SqliteConnectionFactory factory = new(paths);

        using SqliteConnection connection = factory.OpenInitializedConnection();

        Assert.True(Directory.Exists(paths.DataDirectory));
        Assert.True(Directory.Exists(paths.ImagesDirectory));
        Assert.True(Directory.Exists(paths.LogsDirectory));
        Assert.True(File.Exists(paths.DatabasePath));
        Assert.Equal(System.Data.ConnectionState.Open, connection.State);
    }

    [Fact]
    public void DiskDatabasePersistsAfterConnectionIsClosedAndReopened()
    {
        using TemporaryDirectory temporaryDirectory = new();
        AppDataPaths paths = new(temporaryDirectory.Path);
        SqliteConnectionFactory factory = new(paths);
        HistoryItem expected = new(
            Guid.NewGuid(),
            ClipboardContentType.Text,
            "persistent-hash",
            Timestamp,
            Timestamp,
            Timestamp,
            textContent: "persistent text");

        using (SqliteConnection firstConnection = factory.OpenInitializedConnection())
        {
            new SqliteHistoryRepository(firstConnection).Add(expected);
        }

        using SqliteConnection secondConnection = factory.OpenInitializedConnection();
        HistoryItem? actual = new SqliteHistoryRepository(secondConnection).GetById(expected.Id);

        Assert.NotNull(actual);
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.TextContent, actual.TextContent);
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
