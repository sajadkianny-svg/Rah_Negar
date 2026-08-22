using System.Data.Common;
using Microsoft.Data.Sqlite;
using Rah_Negar.Foundation.Application.Transactions;

namespace Rah_Negar.Infrastructure.Database;

public sealed class SqliteTransactionManager : ITransactionManager
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public SqliteTransactionManager(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<T> ExecuteAsync<T>(
        Func<ITransactionContext, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await using SqliteConnection connection =
            await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteTransaction transaction =
            connection.BeginTransaction(deferred: false);
        var context = new SqliteTransactionContext(connection, transaction);

        try
        {
            T result = await operation(context, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Preserve the original operation exception. Disposal closes the connection.
            }

            throw;
        }
    }

    private sealed record SqliteTransactionContext(
        SqliteConnection SqliteConnection,
        SqliteTransaction SqliteTransaction) : ITransactionContext
    {
        public DbConnection Connection => SqliteConnection;
        public DbTransaction Transaction => SqliteTransaction;
    }
}
