using Microsoft.Data.Sqlite;

namespace Rah_Negar.Infrastructure.Database.Readiness;

/// <summary>
/// Opens the existing application database with SQLite's read-only open mode.
/// It deliberately performs no directory creation, migration, transaction, or PRAGMA mutation.
/// </summary>
public interface IPilotReadOnlySqliteConnectionFactory
{
    ValueTask<SqliteConnection> OpenReadOnlyAsync(
        CancellationToken cancellationToken = default);
}

public sealed class PilotReadOnlySqliteConnectionFactory :
    IPilotReadOnlySqliteConnectionFactory
{
    private readonly string _databasePath;

    public PilotReadOnlySqliteConnectionFactory(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _databasePath = Path.GetFullPath(databasePath);
    }

    public async ValueTask<SqliteConnection> OpenReadOnlyAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(_databasePath))
            throw new FileNotFoundException("The Pilot data source is unavailable.");

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
            DefaultTimeout = 10
        };

        var connection = new SqliteConnection(builder.ToString());
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public bool CreatesDatabase => false;
    public bool CreatesDirectory => false;
    public bool OpensWriteTransaction => false;
    public bool ExecutesMigration => false;
    public bool ChangesPragmaState => false;
}
