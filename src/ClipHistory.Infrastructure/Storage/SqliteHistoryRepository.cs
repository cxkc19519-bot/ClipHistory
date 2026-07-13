using System.Data;
using System.Globalization;
using ClipHistory.Core.Models;
using Microsoft.Data.Sqlite;

namespace ClipHistory.Infrastructure.Storage;

public sealed class SqliteHistoryRepository
{
    private readonly SqliteConnection connection;

    public SqliteHistoryRepository(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (connection.State != ConnectionState.Open)
        {
            throw new InvalidOperationException("The SQLite connection must be open.");
        }

        this.connection = connection;
    }

    public void Add(HistoryItem historyItem)
    {
        ArgumentNullException.ThrowIfNull(historyItem);

        using SqliteTransaction transaction = connection.BeginTransaction();
        InsertHistoryItem(historyItem, transaction);
        InsertFilePaths(historyItem, transaction);
        transaction.Commit();
    }

    public HistoryItem? GetById(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The history item ID cannot be empty.", nameof(id));
        }

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                ContentType,
                ContentHash,
                TextContent,
                ImageRelativePath,
                CreatedAtUtc,
                LastCopiedAtUtc,
                RetentionBaseAtUtc,
                IsPinned
            FROM HistoryItems
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", id.ToString("D", CultureInfo.InvariantCulture));

        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        ClipboardContentType contentType = (ClipboardContentType)reader.GetInt32(0);
        string contentHash = reader.GetString(1);
        string? textContent = reader.IsDBNull(2) ? null : reader.GetString(2);
        string? imageRelativePath = reader.IsDBNull(3) ? null : reader.GetString(3);
        DateTimeOffset createdAtUtc = ParseTimestamp(reader.GetString(4));
        DateTimeOffset lastCopiedAtUtc = ParseTimestamp(reader.GetString(5));
        DateTimeOffset retentionBaseAtUtc = ParseTimestamp(reader.GetString(6));
        bool isPinned = reader.GetInt64(7) == 1;
        reader.Close();

        IReadOnlyList<string> filePaths = contentType == ClipboardContentType.Files
            ? GetFilePaths(id)
            : [];

        return new HistoryItem(
            id,
            contentType,
            contentHash,
            createdAtUtc,
            lastCopiedAtUtc,
            retentionBaseAtUtc,
            isPinned,
            textContent,
            imageRelativePath,
            filePaths);
    }

    public HistoryItem? GetByContentFingerprint(
        ClipboardContentType contentType,
        string contentHash)
    {
        if (!Enum.IsDefined(contentType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(contentType),
                contentType,
                "The clipboard content type is not supported.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id
            FROM HistoryItems
            WHERE ContentType = $contentType AND ContentHash = $contentHash;
            """;
        command.Parameters.AddWithValue("$contentType", (int)contentType);
        command.Parameters.AddWithValue("$contentHash", contentHash);

        string? storedId = command.ExecuteScalar() as string;
        return storedId is null
            ? null
            : GetById(Guid.ParseExact(storedId, "D"));
    }

    public bool UpdateState(HistoryItem historyItem)
    {
        ArgumentNullException.ThrowIfNull(historyItem);

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            UPDATE HistoryItems
            SET
                LastCopiedAtUtc = $lastCopiedAtUtc,
                RetentionBaseAtUtc = $retentionBaseAtUtc,
                IsPinned = $isPinned
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$lastCopiedAtUtc", FormatTimestamp(historyItem.LastCopiedAtUtc));
        command.Parameters.AddWithValue(
            "$retentionBaseAtUtc",
            FormatTimestamp(historyItem.RetentionBaseAtUtc));
        command.Parameters.AddWithValue("$isPinned", historyItem.IsPinned ? 1 : 0);
        command.Parameters.AddWithValue(
            "$id",
            historyItem.Id.ToString("D", CultureInfo.InvariantCulture));

        return command.ExecuteNonQuery() == 1;
    }

    public IReadOnlyList<HistoryItem> GetAll()
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                Id,
                ContentType,
                ContentHash,
                TextContent,
                ImageRelativePath,
                CreatedAtUtc,
                LastCopiedAtUtc,
                RetentionBaseAtUtc,
                IsPinned
            FROM HistoryItems
            ORDER BY
                IsPinned DESC,
                LastCopiedAtUtc DESC,
                CreatedAtUtc DESC,
                Id ASC;
            """;

        using SqliteDataReader reader = command.ExecuteReader();
        List<StoredHistoryItem> storedItems = [];
        while (reader.Read())
        {
            storedItems.Add(new StoredHistoryItem(
                Guid.ParseExact(reader.GetString(0), "D"),
                (ClipboardContentType)reader.GetInt32(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                ParseTimestamp(reader.GetString(5)),
                ParseTimestamp(reader.GetString(6)),
                ParseTimestamp(reader.GetString(7)),
                reader.GetInt64(8) == 1));
        }

        reader.Close();
        Dictionary<Guid, List<string>> filePathsByItem = GetAllFilePaths();
        List<HistoryItem> historyItems = new(storedItems.Count);

        foreach (StoredHistoryItem storedItem in storedItems)
        {
            IReadOnlyList<string> filePaths = filePathsByItem.TryGetValue(
                storedItem.Id,
                out List<string>? storedPaths)
                ? storedPaths
                : [];

            historyItems.Add(new HistoryItem(
                storedItem.Id,
                storedItem.ContentType,
                storedItem.ContentHash,
                storedItem.CreatedAtUtc,
                storedItem.LastCopiedAtUtc,
                storedItem.RetentionBaseAtUtc,
                storedItem.IsPinned,
                storedItem.TextContent,
                storedItem.ImageRelativePath,
                filePaths));
        }

        return historyItems;
    }

    public bool Delete(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The history item ID cannot be empty.", nameof(id));
        }

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM HistoryItems WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString("D", CultureInfo.InvariantCulture));
        return command.ExecuteNonQuery() == 1;
    }

    public int DeleteMany(IEnumerable<Guid> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        Guid[] uniqueIds = ids.Distinct().ToArray();
        if (uniqueIds.Any(id => id == Guid.Empty))
        {
            throw new ArgumentException("History item IDs cannot be empty.", nameof(ids));
        }

        using SqliteTransaction transaction = connection.BeginTransaction();
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM HistoryItems WHERE Id = $id;";
        SqliteParameter idParameter = command.Parameters.Add("$id", SqliteType.Text);
        int deletedCount = 0;

        foreach (Guid id in uniqueIds)
        {
            idParameter.Value = id.ToString("D", CultureInfo.InvariantCulture);
            deletedCount += command.ExecuteNonQuery();
        }

        transaction.Commit();
        return deletedCount;
    }

    private void InsertHistoryItem(HistoryItem historyItem, SqliteTransaction transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO HistoryItems (
                Id,
                ContentType,
                ContentHash,
                TextContent,
                ImageRelativePath,
                CreatedAtUtc,
                LastCopiedAtUtc,
                RetentionBaseAtUtc,
                IsPinned)
            VALUES (
                $id,
                $contentType,
                $contentHash,
                $textContent,
                $imageRelativePath,
                $createdAtUtc,
                $lastCopiedAtUtc,
                $retentionBaseAtUtc,
                $isPinned);
            """;
        command.Parameters.AddWithValue(
            "$id",
            historyItem.Id.ToString("D", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$contentType", (int)historyItem.ContentType);
        command.Parameters.AddWithValue("$contentHash", historyItem.ContentHash);
        command.Parameters.AddWithValue("$textContent", (object?)historyItem.TextContent ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$imageRelativePath",
            (object?)historyItem.ImageRelativePath ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAtUtc", FormatTimestamp(historyItem.CreatedAtUtc));
        command.Parameters.AddWithValue("$lastCopiedAtUtc", FormatTimestamp(historyItem.LastCopiedAtUtc));
        command.Parameters.AddWithValue(
            "$retentionBaseAtUtc",
            FormatTimestamp(historyItem.RetentionBaseAtUtc));
        command.Parameters.AddWithValue("$isPinned", historyItem.IsPinned ? 1 : 0);
        command.ExecuteNonQuery();
    }

    private void InsertFilePaths(HistoryItem historyItem, SqliteTransaction transaction)
    {
        if (historyItem.ContentType != ClipboardContentType.Files)
        {
            return;
        }

        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO HistoryItemFiles (HistoryItemId, Position, FilePath)
            VALUES ($historyItemId, $position, $filePath);
            """;
        command.Parameters.AddWithValue(
            "$historyItemId",
            historyItem.Id.ToString("D", CultureInfo.InvariantCulture));
        SqliteParameter positionParameter = command.Parameters.Add("$position", SqliteType.Integer);
        SqliteParameter pathParameter = command.Parameters.Add("$filePath", SqliteType.Text);

        for (int index = 0; index < historyItem.FilePaths.Count; index++)
        {
            positionParameter.Value = index;
            pathParameter.Value = historyItem.FilePaths[index];
            command.ExecuteNonQuery();
        }
    }

    private List<string> GetFilePaths(Guid historyItemId)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT FilePath
            FROM HistoryItemFiles
            WHERE HistoryItemId = $historyItemId
            ORDER BY Position;
            """;
        command.Parameters.AddWithValue(
            "$historyItemId",
            historyItemId.ToString("D", CultureInfo.InvariantCulture));

        using SqliteDataReader reader = command.ExecuteReader();
        List<string> filePaths = [];
        while (reader.Read())
        {
            filePaths.Add(reader.GetString(0));
        }

        return filePaths;
    }

    private Dictionary<Guid, List<string>> GetAllFilePaths()
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT HistoryItemId, FilePath
            FROM HistoryItemFiles
            ORDER BY HistoryItemId, Position;
            """;

        using SqliteDataReader reader = command.ExecuteReader();
        Dictionary<Guid, List<string>> filePathsByItem = [];
        while (reader.Read())
        {
            Guid historyItemId = Guid.ParseExact(reader.GetString(0), "D");
            if (!filePathsByItem.TryGetValue(historyItemId, out List<string>? filePaths))
            {
                filePaths = [];
                filePathsByItem.Add(historyItemId, filePaths);
            }

            filePaths.Add(reader.GetString(1));
        }

        return filePathsByItem;
    }

    private static string FormatTimestamp(DateTimeOffset value)
    {
        return value.ToString("O", CultureInfo.InvariantCulture);
    }

    private static DateTimeOffset ParseTimestamp(string value)
    {
        return DateTimeOffset.ParseExact(
            value,
            "O",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None);
    }

    private sealed record StoredHistoryItem(
        Guid Id,
        ClipboardContentType ContentType,
        string ContentHash,
        string? TextContent,
        string? ImageRelativePath,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset LastCopiedAtUtc,
        DateTimeOffset RetentionBaseAtUtc,
        bool IsPinned);
}
