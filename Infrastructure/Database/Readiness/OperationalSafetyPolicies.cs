using Microsoft.Data.Sqlite;
using Rah_Negar.Foundation.Application.Database.Readiness;

namespace Rah_Negar.Infrastructure.Database.Readiness;

public sealed class SqliteBusyRetryExecutor : ISqliteBusyRetryExecutor
{
    private readonly SqliteLockBusyPolicy _policy;

    public SqliteBusyRetryExecutor(SqliteLockBusyPolicy policy) =>
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));

    public async Task<T> ExecuteAsync<T>(Func<int, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        for (int attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await operation(attempt, cancellationToken).ConfigureAwait(false);
            }
            catch (SqliteException ex) when (IsBusy(ex) && attempt < _policy.MaximumRetryCount)
            {
                TimeSpan delay = _policy.RetryDelayPolicy.GetDelay(attempt + 1);
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static bool IsBusy(SqliteException exception) => exception.SqliteErrorCode is 5 or 6;
}

public static class SqliteLockReadinessEvaluator
{
    public static SqliteLockReadinessResult Evaluate(SqliteLockBusyPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        return new(true, policy.BusyTimeout, policy.MaximumRetryCount, "BoundedFailClosedPolicy");
    }
}

public sealed class DriveDiskCapacityProvider : IDiskCapacityProvider
{
    public Task<long?> GetAvailableBytesAsync(string explicitDestinationPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(explicitDestinationPath);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            string full = Path.GetFullPath(explicitDestinationPath);
            string? root = Path.GetPathRoot(full);
            if (string.IsNullOrWhiteSpace(root)) return Task.FromResult<long?>(null);
            var drive = new DriveInfo(root);
            return Task.FromResult<long?>(drive.IsReady ? drive.AvailableFreeSpace : null);
        }
        catch
        {
            return Task.FromResult<long?>(null);
        }
    }
}
