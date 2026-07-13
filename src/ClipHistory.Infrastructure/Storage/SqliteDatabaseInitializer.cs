using System.Data;
using Microsoft.Data.Sqlite;

namespace ClipHistory.Infrastructure.Storage;

public static class SqliteDatabaseInitializer
{
    public const int CurrentSchemaVersion = 1;

    public static void Initialize(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (connection.State != ConnectionState.Open)
        {
            throw new InvalidOperationException("The SQLite connection must be open before initialization.");
        }

        using (SqliteCommand foreignKeysCommand = connection.CreateCommand())
        {
            foreignKeysCommand.CommandText = "PRAGMA foreign_keys = ON;";
            foreignKeysCommand.ExecuteNonQuery();
        }

        using SqliteTransaction transaction = connection.BeginTransaction();
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = InitialSchemaSql;
        command.ExecuteNonQuery();

        command.CommandText = "SELECT Version FROM SchemaInfo WHERE Id = 1;";
        object? storedVersion = command.ExecuteScalar();
        int schemaVersion = Convert.ToInt32(storedVersion, System.Globalization.CultureInfo.InvariantCulture);

        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported database schema version {schemaVersion}. Expected {CurrentSchemaVersion}.");
        }

        transaction.Commit();
    }

    private const string InitialSchemaSql = """
        CREATE TABLE IF NOT EXISTS SchemaInfo (
            Id INTEGER NOT NULL PRIMARY KEY CHECK (Id = 1),
            Version INTEGER NOT NULL CHECK (Version > 0)
        );

        INSERT INTO SchemaInfo (Id, Version)
        VALUES (1, 1)
        ON CONFLICT (Id) DO NOTHING;

        CREATE TABLE IF NOT EXISTS HistoryItems (
            Id TEXT NOT NULL PRIMARY KEY,
            ContentType INTEGER NOT NULL CHECK (ContentType IN (1, 2, 3)),
            ContentHash TEXT NOT NULL,
            TextContent TEXT NULL,
            ImageRelativePath TEXT NULL,
            CreatedAtUtc TEXT NOT NULL,
            LastCopiedAtUtc TEXT NOT NULL,
            RetentionBaseAtUtc TEXT NOT NULL,
            IsPinned INTEGER NOT NULL CHECK (IsPinned IN (0, 1)),
            CHECK (
                (ContentType = 1 AND TextContent IS NOT NULL AND ImageRelativePath IS NULL)
                OR (ContentType = 2 AND TextContent IS NULL AND ImageRelativePath IS NOT NULL)
                OR (ContentType = 3 AND TextContent IS NULL AND ImageRelativePath IS NULL)
            )
        );

        CREATE UNIQUE INDEX IF NOT EXISTS IX_HistoryItems_ContentHash
            ON HistoryItems (ContentType, ContentHash);

        CREATE INDEX IF NOT EXISTS IX_HistoryItems_DisplayOrder
            ON HistoryItems (IsPinned DESC, LastCopiedAtUtc DESC, CreatedAtUtc DESC, Id ASC);

        CREATE INDEX IF NOT EXISTS IX_HistoryItems_Retention
            ON HistoryItems (IsPinned, RetentionBaseAtUtc);

        CREATE TABLE IF NOT EXISTS HistoryItemFiles (
            HistoryItemId TEXT NOT NULL,
            Position INTEGER NOT NULL CHECK (Position >= 0),
            FilePath TEXT NOT NULL,
            PRIMARY KEY (HistoryItemId, Position),
            FOREIGN KEY (HistoryItemId) REFERENCES HistoryItems (Id) ON DELETE CASCADE
        );

        CREATE UNIQUE INDEX IF NOT EXISTS IX_HistoryItemFiles_ItemPath
            ON HistoryItemFiles (HistoryItemId, FilePath COLLATE NOCASE);
        """;
}
