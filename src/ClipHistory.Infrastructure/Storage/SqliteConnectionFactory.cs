using Microsoft.Data.Sqlite;

namespace ClipHistory.Infrastructure.Storage;

public sealed class SqliteConnectionFactory
{
    private readonly AppDataPaths paths;

    public SqliteConnectionFactory(AppDataPaths paths)
    {
        this.paths = paths ?? throw new ArgumentNullException(nameof(paths));
    }

    public SqliteConnection OpenInitializedConnection()
    {
        paths.EnsureDirectoriesExist();

        SqliteConnectionStringBuilder connectionString = new()
        {
            DataSource = paths.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            DefaultTimeout = 5,
            Pooling = false,
        };

        SqliteConnection connection = new(connectionString.ToString());
        try
        {
            connection.Open();
            SqliteDatabaseInitializer.Initialize(connection);
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }
}
