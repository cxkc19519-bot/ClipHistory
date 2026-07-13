using ClipHistory.Core.Models;
using ClipHistory.Infrastructure.Storage;
using Microsoft.Data.Sqlite;

namespace ClipHistory.Infrastructure.Tests.Storage;

public sealed class SqliteHistoryRepositoryTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 7, 12, 4, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AddAndGetByIdRoundTripsTextItem()
    {
        using SqliteConnection connection = CreateDatabase();
        SqliteHistoryRepository repository = new(connection);
        HistoryItem expected = new(
            Guid.NewGuid(),
            ClipboardContentType.Text,
            "text-hash",
            CreatedAtUtc,
            CreatedAtUtc.AddMinutes(1),
            CreatedAtUtc.AddMinutes(1),
            isPinned: true,
            textContent: "hello\r\n世界");

        repository.Add(expected);
        HistoryItem? actual = repository.GetById(expected.Id);

        AssertHistoryItem(expected, actual);
    }

    [Fact]
    public void AddAndGetByIdRoundTripsImageMetadata()
    {
        using SqliteConnection connection = CreateDatabase();
        SqliteHistoryRepository repository = new(connection);
        HistoryItem expected = new(
            Guid.NewGuid(),
            ClipboardContentType.Image,
            "image-hash",
            CreatedAtUtc,
            CreatedAtUtc,
            CreatedAtUtc,
            imageRelativePath: "images/2026/image.png");

        repository.Add(expected);
        HistoryItem? actual = repository.GetById(expected.Id);

        AssertHistoryItem(expected, actual);
    }

    [Fact]
    public void AddAndGetByIdRoundTripsOrderedFilePaths()
    {
        using SqliteConnection connection = CreateDatabase();
        SqliteHistoryRepository repository = new(connection);
        HistoryItem expected = new(
            Guid.NewGuid(),
            ClipboardContentType.Files,
            "files-hash",
            CreatedAtUtc,
            CreatedAtUtc,
            CreatedAtUtc,
            filePaths: [@"C:\Files\one.txt", @"D:\图片\two.png"]);

        repository.Add(expected);
        HistoryItem? actual = repository.GetById(expected.Id);

        AssertHistoryItem(expected, actual);
    }

    [Fact]
    public void GetByIdReturnsNullWhenItemDoesNotExist()
    {
        using SqliteConnection connection = CreateDatabase();
        SqliteHistoryRepository repository = new(connection);

        HistoryItem? actual = repository.GetById(Guid.NewGuid());

        Assert.Null(actual);
    }

    [Fact]
    public void AddRollsBackMainRowWhenAFilePathInsertFails()
    {
        using SqliteConnection connection = CreateDatabase();
        SqliteHistoryRepository repository = new(connection);
        HistoryItem invalidForStorage = new(
            Guid.NewGuid(),
            ClipboardContentType.Files,
            "files-hash",
            CreatedAtUtc,
            CreatedAtUtc,
            CreatedAtUtc,
            filePaths: [@"C:\Files\one.txt", @"c:\files\ONE.TXT"]);

        Assert.Throws<SqliteException>(() => repository.Add(invalidForStorage));
        Assert.Null(repository.GetById(invalidForStorage.Id));
    }

    [Fact]
    public void GetByContentFingerprintReturnsMatchingTypeAndHash()
    {
        using SqliteConnection connection = CreateDatabase();
        SqliteHistoryRepository repository = new(connection);
        HistoryItem expected = CreateTextItem("shared-hash");
        repository.Add(expected);

        HistoryItem? matching = repository.GetByContentFingerprint(
            ClipboardContentType.Text,
            "shared-hash");
        HistoryItem? wrongType = repository.GetByContentFingerprint(
            ClipboardContentType.Image,
            "shared-hash");
        HistoryItem? wrongHash = repository.GetByContentFingerprint(
            ClipboardContentType.Text,
            "other-hash");

        AssertHistoryItem(expected, matching);
        Assert.Null(wrongType);
        Assert.Null(wrongHash);
    }

    [Fact]
    public void UpdateStatePersistsRepeatedCopyAndPin()
    {
        using SqliteConnection connection = CreateDatabase();
        SqliteHistoryRepository repository = new(connection);
        HistoryItem item = CreateTextItem("text-hash");
        repository.Add(item);
        DateTimeOffset copiedAtUtc = CreatedAtUtc.AddMinutes(10);

        item.MarkCopied(copiedAtUtc);
        item.Pin();
        bool updated = repository.UpdateState(item);
        HistoryItem? stored = repository.GetById(item.Id);

        Assert.True(updated);
        HistoryItem actual = Assert.IsType<HistoryItem>(stored);
        AssertHistoryItem(item, actual);
        Assert.Equal(copiedAtUtc, actual.RetentionBaseAtUtc);
        Assert.True(actual.IsPinned);
    }

    [Fact]
    public void UpdateStatePersistsUnpinRetentionReset()
    {
        using SqliteConnection connection = CreateDatabase();
        SqliteHistoryRepository repository = new(connection);
        HistoryItem item = CreateTextItem("text-hash", isPinned: true);
        repository.Add(item);
        DateTimeOffset unpinnedAtUtc = CreatedAtUtc.AddHours(2);

        item.Unpin(unpinnedAtUtc);
        bool updated = repository.UpdateState(item);
        HistoryItem? stored = repository.GetById(item.Id);

        Assert.True(updated);
        HistoryItem actual = Assert.IsType<HistoryItem>(stored);
        AssertHistoryItem(item, actual);
        Assert.Equal(unpinnedAtUtc, actual.RetentionBaseAtUtc);
        Assert.False(actual.IsPinned);
    }

    [Fact]
    public void UpdateStateReturnsFalseWhenItemDoesNotExist()
    {
        using SqliteConnection connection = CreateDatabase();
        SqliteHistoryRepository repository = new(connection);
        HistoryItem missing = CreateTextItem("missing-hash");

        bool updated = repository.UpdateState(missing);

        Assert.False(updated);
    }

    [Fact]
    public void GetAllReturnsEmptyCollectionForEmptyDatabase()
    {
        using SqliteConnection connection = CreateDatabase();
        SqliteHistoryRepository repository = new(connection);

        IReadOnlyList<HistoryItem> items = repository.GetAll();

        Assert.Empty(items);
    }

    [Fact]
    public void GetAllReturnsPinnedFirstAndNewestFirstWithinEachGroup()
    {
        using SqliteConnection connection = CreateDatabase();
        SqliteHistoryRepository repository = new(connection);
        HistoryItem olderPinned = CreateOrderedTextItem(1, 1, isPinned: true);
        HistoryItem newerPinned = CreateOrderedTextItem(2, 3, isPinned: true);
        HistoryItem olderRegular = CreateOrderedTextItem(3, 0, isPinned: false);
        HistoryItem newerFiles = new(
            new Guid("00000000-0000-0000-0000-000000000004"),
            ClipboardContentType.Files,
            "hash-4",
            CreatedAtUtc,
            CreatedAtUtc.AddMinutes(4),
            CreatedAtUtc.AddMinutes(4),
            filePaths: [@"C:\Files\one.txt", @"D:\图片\two.png"]);

        repository.Add(olderRegular);
        repository.Add(newerFiles);
        repository.Add(olderPinned);
        repository.Add(newerPinned);

        IReadOnlyList<HistoryItem> items = repository.GetAll();

        Assert.Equal(
            [newerPinned.Id, olderPinned.Id, newerFiles.Id, olderRegular.Id],
            items.Select(item => item.Id));
        Assert.Equal(newerFiles.FilePaths, items[2].FilePaths);
    }

    [Fact]
    public void DeleteRemovesItemAndCascadesFilePathRows()
    {
        using SqliteConnection connection = CreateDatabase();
        SqliteHistoryRepository repository = new(connection);
        HistoryItem files = new(
            Guid.NewGuid(),
            ClipboardContentType.Files,
            "files-hash",
            CreatedAtUtc,
            CreatedAtUtc,
            CreatedAtUtc,
            filePaths: [@"C:\Files\one.txt", @"D:\Files\two.txt"]);
        repository.Add(files);

        bool deleted = repository.Delete(files.Id);

        Assert.True(deleted);
        Assert.Null(repository.GetById(files.Id));
        Assert.Equal(0L, ExecuteInt64(connection, "SELECT COUNT(*) FROM HistoryItemFiles;"));
    }

    [Fact]
    public void DeleteReturnsFalseWhenItemDoesNotExist()
    {
        using SqliteConnection connection = CreateDatabase();
        SqliteHistoryRepository repository = new(connection);

        bool deleted = repository.Delete(Guid.NewGuid());

        Assert.False(deleted);
    }

    [Fact]
    public void DeletedItemCanBeReaddedForUndo()
    {
        using SqliteConnection connection = CreateDatabase();
        SqliteHistoryRepository repository = new(connection);
        HistoryItem snapshot = CreateTextItem("undo-hash", isPinned: true);
        repository.Add(snapshot);

        Assert.True(repository.Delete(snapshot.Id));
        repository.Add(snapshot);
        HistoryItem? restored = repository.GetById(snapshot.Id);

        AssertHistoryItem(snapshot, restored);
    }

    [Fact]
    public void DeleteManyRemovesRequestedItemsInSingleOperation()
    {
        using SqliteConnection connection = CreateDatabase();
        SqliteHistoryRepository repository = new(connection);
        HistoryItem first = CreateTextItem("first-hash");
        HistoryItem second = CreateTextItem("second-hash", isPinned: true);
        HistoryItem kept = CreateTextItem("kept-hash");
        repository.Add(first);
        repository.Add(second);
        repository.Add(kept);

        int deleted = repository.DeleteMany([first.Id, second.Id, first.Id]);

        Assert.Equal(2, deleted);
        Assert.Null(repository.GetById(first.Id));
        Assert.Null(repository.GetById(second.Id));
        Assert.NotNull(repository.GetById(kept.Id));
    }

    private static SqliteConnection CreateDatabase()
    {
        SqliteConnection connection = new("Data Source=:memory:");
        connection.Open();
        SqliteDatabaseInitializer.Initialize(connection);
        return connection;
    }

    private static void AssertHistoryItem(HistoryItem expected, HistoryItem? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.ContentType, actual.ContentType);
        Assert.Equal(expected.ContentHash, actual.ContentHash);
        Assert.Equal(expected.TextContent, actual.TextContent);
        Assert.Equal(expected.ImageRelativePath, actual.ImageRelativePath);
        Assert.Equal(expected.CreatedAtUtc, actual.CreatedAtUtc);
        Assert.Equal(expected.LastCopiedAtUtc, actual.LastCopiedAtUtc);
        Assert.Equal(expected.RetentionBaseAtUtc, actual.RetentionBaseAtUtc);
        Assert.Equal(expected.IsPinned, actual.IsPinned);
        Assert.Equal(expected.FilePaths, actual.FilePaths);
    }

    private static HistoryItem CreateTextItem(string contentHash, bool isPinned = false)
    {
        return new HistoryItem(
            Guid.NewGuid(),
            ClipboardContentType.Text,
            contentHash,
            CreatedAtUtc,
            CreatedAtUtc,
            CreatedAtUtc,
            isPinned,
            textContent: "hello");
    }

    private static HistoryItem CreateOrderedTextItem(
        int idSuffix,
        int copiedAtMinute,
        bool isPinned)
    {
        return new HistoryItem(
            new Guid($"00000000-0000-0000-0000-{idSuffix:D12}"),
            ClipboardContentType.Text,
            $"hash-{idSuffix}",
            CreatedAtUtc,
            CreatedAtUtc.AddMinutes(copiedAtMinute),
            CreatedAtUtc.AddMinutes(copiedAtMinute),
            isPinned,
            textContent: $"item-{idSuffix}");
    }

    private static long ExecuteInt64(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }
}
