using ClipHistory.Infrastructure.Storage;
using Microsoft.Data.Sqlite;

namespace ClipHistory.Infrastructure.Tests.Storage;

public sealed class SqliteDatabaseInitializerTests
{
    [Fact]
    public void InitializeCreatesVersionedInitialSchema()
    {
        using SqliteConnection connection = CreateOpenInMemoryConnection();

        SqliteDatabaseInitializer.Initialize(connection);

        Assert.Equal(1L, ExecuteInt64(connection, "SELECT Version FROM SchemaInfo WHERE Id = 1;"));
        Assert.Equal(1L, TableExists(connection, "HistoryItems"));
        Assert.Equal(1L, TableExists(connection, "HistoryItemFiles"));
    }

    [Fact]
    public void InitializeCanRunMoreThanOnceWithoutChangingSchemaVersion()
    {
        using SqliteConnection connection = CreateOpenInMemoryConnection();

        SqliteDatabaseInitializer.Initialize(connection);
        SqliteDatabaseInitializer.Initialize(connection);

        Assert.Equal(1L, ExecuteInt64(connection, "SELECT COUNT(*) FROM SchemaInfo;"));
        Assert.Equal(1L, ExecuteInt64(connection, "SELECT Version FROM SchemaInfo WHERE Id = 1;"));
    }

    [Fact]
    public void InitializeRejectsUnsupportedExistingSchemaVersion()
    {
        using SqliteConnection connection = CreateOpenInMemoryConnection();
        SqliteDatabaseInitializer.Initialize(connection);

        ExecuteNonQuery(connection, "UPDATE SchemaInfo SET Version = 999 WHERE Id = 1;");

        Assert.Throws<InvalidOperationException>(
            () => SqliteDatabaseInitializer.Initialize(connection));
    }

    [Fact]
    public void InitializeRequiresOpenConnection()
    {
        using SqliteConnection connection = new("Data Source=:memory:");

        Assert.Throws<InvalidOperationException>(
            () => SqliteDatabaseInitializer.Initialize(connection));
    }

    [Fact]
    public void ForeignKeyCascadeRemovesAssociatedFileRows()
    {
        using SqliteConnection connection = CreateOpenInMemoryConnection();
        SqliteDatabaseInitializer.Initialize(connection);
        ExecuteNonQuery(connection, """
            INSERT INTO HistoryItems (
                Id, ContentType, ContentHash, TextContent, ImageRelativePath,
                CreatedAtUtc, LastCopiedAtUtc, RetentionBaseAtUtc, IsPinned)
            VALUES (
                '00000000-0000-0000-0000-000000000001', 3, 'files-hash', NULL, NULL,
                '2026-07-12T04:00:00.0000000+00:00',
                '2026-07-12T04:00:00.0000000+00:00',
                '2026-07-12T04:00:00.0000000+00:00', 0);

            INSERT INTO HistoryItemFiles (HistoryItemId, Position, FilePath)
            VALUES ('00000000-0000-0000-0000-000000000001', 0, 'C:\Files\one.txt');

            DELETE FROM HistoryItems
            WHERE Id = '00000000-0000-0000-0000-000000000001';
            """);

        Assert.Equal(0L, ExecuteInt64(connection, "SELECT COUNT(*) FROM HistoryItemFiles;"));
    }

    [Fact]
    public void InitializeEnablesForeignKeyEnforcement()
    {
        using SqliteConnection connection = CreateOpenInMemoryConnection();

        SqliteDatabaseInitializer.Initialize(connection);

        Assert.Equal(1L, ExecuteInt64(connection, "PRAGMA foreign_keys;"));
    }

    private static SqliteConnection CreateOpenInMemoryConnection()
    {
        SqliteConnection connection = new("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    private static long TableExists(SqliteConnection connection, string tableName)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table' AND name = $tableName;
            """;
        command.Parameters.AddWithValue("$tableName", tableName);
        return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static long ExecuteInt64(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void ExecuteNonQuery(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
