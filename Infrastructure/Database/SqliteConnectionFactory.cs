using Microsoft.Data.Sqlite;

namespace Rah_Negar.Infrastructure.Database;

public sealed class SqliteConnectionFactory : ISqliteConnectionFactory
{
    private const string StandardPragmas = """
        PRAGMA foreign_keys = ON;
        PRAGMA journal_mode = WAL;
        PRAGMA synchronous = NORMAL;
        PRAGMA temp_store = MEMORY;
        """;

    private readonly SqliteDatabaseOptions _options;

    public SqliteConnectionFactory(SqliteDatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        _options = options;
    }

    public async ValueTask<SqliteConnection> OpenConnectionAsync(
        CancellationToken cancellationToken = default)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(_options.DataSource));
        if (_options.Mode == SqliteOpenMode.ReadWriteCreate && !string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _options.DataSource,
            Mode = _options.Mode,
            Cache = _options.Cache,
            Pooling = _options.Pooling,
            DefaultTimeout = _options.DefaultTimeoutSeconds
        };

        var connection = new SqliteConnection(builder.ToString());
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = StandardPragmas;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
