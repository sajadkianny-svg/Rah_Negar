using Microsoft.Data.Sqlite;

namespace Rah_Negar.Infrastructure.Database;

public interface ISqliteConnectionFactory
{
    ValueTask<SqliteConnection> OpenConnectionAsync(
        CancellationToken cancellationToken = default);
}
