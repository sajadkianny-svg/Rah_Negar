using System.Globalization;
using Microsoft.Data.Sqlite;
using Rah_Negar.Foundation.Application.Security;
using Rah_Negar.Infrastructure.Database;

namespace Rah_Negar.Infrastructure.Security;

public enum AtomicEsdFailurePoint
{
    None,
    AfterReplayCheck,
    AfterConsumeInsert,
    AfterSettingMutation,
    AfterReceiptInsert,
    BeforeCommit
}

public interface IAtomicEsdFailureInjector
{
    void ThrowIfRequested(AtomicEsdFailurePoint point);
}

public sealed class NoAtomicEsdFailureInjector : IAtomicEsdFailureInjector
{
    public static NoAtomicEsdFailureInjector Instance { get; } = new();
    private NoAtomicEsdFailureInjector() { }
    public void ThrowIfRequested(AtomicEsdFailurePoint point) { }
}

/// <summary>
/// Inactive SQLite adapter. SecurityDeploymentSettings is the authoritative target-architecture
/// ESD value; the legacy production app_settings table is never read or written here.
/// </summary>
public sealed class SQLiteAtomicEsdAdjustmentExecutionBoundary : IAtomicEsdAdjustmentExecutionBoundary
{
    private readonly ISqliteConnectionFactory _connections;
    private readonly IAtomicEsdFailureInjector _failures;

    public SQLiteAtomicEsdAdjustmentExecutionBoundary(ISqliteConnectionFactory connections,
        IAtomicEsdFailureInjector? failures = null)
    {
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _failures = failures ?? NoAtomicEsdFailureInjector.Instance;
    }

    public async Task<AtomicEsdExecutionResult> ExecuteOnceAsync(VendorAuthorizationConsumption consumption,
        decimal proposedEsdAdjustment, Func<CancellationToken, Task> mutation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consumption);
        ArgumentNullException.ThrowIfNull(mutation);
        if (string.IsNullOrWhiteSpace(consumption.ExecutionReceiptId) ||
            string.IsNullOrWhiteSpace(consumption.DeviceId) || string.IsNullOrWhiteSpace(consumption.KeyId) ||
            string.IsNullOrWhiteSpace(consumption.InitiatingShiftProfileId) ||
            consumption.Action != VendorSupportAction.ChangeEsdAdjustment)
            return new(AtomicEsdExecutionStatus.StoreFailed, null);

        await using SqliteConnection connection = await _connections.OpenConnectionAsync(cancellationToken);
        await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            if (await IsConsumedAsync(connection, transaction, consumption.RequestId, cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return new(AtomicEsdExecutionStatus.AlreadyConsumed, null);
            }
            _failures.ThrowIfRequested(AtomicEsdFailurePoint.AfterReplayCheck);

            string canonical = proposedEsdAdjustment.ToString("G29", CultureInfo.InvariantCulture);
            await InsertConsumptionAsync(connection, transaction, consumption, canonical, cancellationToken);
            _failures.ThrowIfRequested(AtomicEsdFailurePoint.AfterConsumeInsert);

            long revision = await UpdateSettingAsync(connection, transaction, consumption,
                canonical, cancellationToken);
            _failures.ThrowIfRequested(AtomicEsdFailurePoint.AfterSettingMutation);

            try { await mutation(cancellationToken).ConfigureAwait(false); }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                return new(AtomicEsdExecutionStatus.MutationFailed, null);
            }

            await InsertReceiptAsync(connection, transaction, consumption, canonical, revision, cancellationToken);
            _failures.ThrowIfRequested(AtomicEsdFailurePoint.AfterReceiptInsert);
            _failures.ThrowIfRequested(AtomicEsdFailurePoint.BeforeCommit);
            await transaction.CommitAsync(cancellationToken);
            return new(AtomicEsdExecutionStatus.Executed, consumption.ExecutionReceiptId);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            await SQLiteShiftProfileCredentialRepository.SafeRollbackAsync(transaction, cancellationToken);
            return await RequestExistsAsync(consumption.RequestId, cancellationToken)
                ? new(AtomicEsdExecutionStatus.AlreadyConsumed, null)
                : new(AtomicEsdExecutionStatus.StoreFailed, null);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode is 5 or 6)
        {
            await SQLiteShiftProfileCredentialRepository.SafeRollbackAsync(transaction, cancellationToken);
            return new(AtomicEsdExecutionStatus.StoreFailed, null);
        }
        catch
        {
            await SQLiteShiftProfileCredentialRepository.SafeRollbackAsync(transaction, cancellationToken);
            return new(AtomicEsdExecutionStatus.StoreFailed, null);
        }
    }

    private async Task<bool> RequestExistsAsync(string requestId, CancellationToken token)
    {
        try
        {
            await using SqliteConnection connection = await _connections.OpenConnectionAsync(token);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT EXISTS(SELECT 1 FROM SecurityConsumedVendorAuthorizations WHERE RequestId=$id);";
            command.Parameters.AddWithValue("$id", requestId);
            return Convert.ToInt32(await command.ExecuteScalarAsync(token)) == 1;
        }
        catch { return false; }
    }

    private static async Task<bool> IsConsumedAsync(SqliteConnection connection, SqliteTransaction transaction,
        string requestId, CancellationToken token)
    {
        await using SqliteCommand command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM SecurityConsumedVendorAuthorizations WHERE RequestId=$id);";
        command.Parameters.AddWithValue("$id", requestId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(token)) == 1;
    }

    private static async Task InsertConsumptionAsync(SqliteConnection connection, SqliteTransaction transaction,
        VendorAuthorizationConsumption value, string proposed, CancellationToken token)
    {
        await using SqliteCommand command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO SecurityConsumedVendorAuthorizations
             (RequestId,CorrelationId,DeviceId,Action,ProposedEsdAdjustmentCanonical,KeyId,ConsumedAtUtc,
              InitiatingShiftProfileId,ExecutionReceiptId,ResultStatus)
            VALUES ($request,$correlation,$device,$action,$proposed,$key,$at,$actor,$receipt,'Succeeded');
            """;
        command.Parameters.AddWithValue("$request",value.RequestId); command.Parameters.AddWithValue("$correlation",value.CorrelationId);
        command.Parameters.AddWithValue("$device",value.DeviceId); command.Parameters.AddWithValue("$action",value.Action.ToString());
        command.Parameters.AddWithValue("$proposed",proposed); command.Parameters.AddWithValue("$key",value.KeyId);
        command.Parameters.AddWithValue("$at",SQLiteShiftProfileRepository.Format(value.ConsumedAtUtc));
        command.Parameters.AddWithValue("$actor",value.InitiatingShiftProfileId); command.Parameters.AddWithValue("$receipt",value.ExecutionReceiptId!);
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task<long> UpdateSettingAsync(SqliteConnection connection, SqliteTransaction transaction,
        VendorAuthorizationConsumption value, string proposed, CancellationToken token)
    {
        await using SqliteCommand command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            UPDATE SecurityDeploymentSettings
            SET EsdAdjustmentCanonical=$value,Revision=Revision+1,UpdatedAtUtc=$at,UpdatedByShiftProfileId=$actor
            WHERE SingletonId=1 RETURNING Revision;
            """;
        command.Parameters.AddWithValue("$value",proposed);
        command.Parameters.AddWithValue("$at",SQLiteShiftProfileRepository.Format(value.ConsumedAtUtc));
        command.Parameters.AddWithValue("$actor",value.InitiatingShiftProfileId);
        object? revision = await command.ExecuteScalarAsync(token);
        return revision is null ? throw new InvalidOperationException("ESD target setting is not provisioned.") : Convert.ToInt64(revision);
    }

    private static async Task InsertReceiptAsync(SqliteConnection connection, SqliteTransaction transaction,
        VendorAuthorizationConsumption value, string proposed, long revision, CancellationToken token)
    {
        await using SqliteCommand command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO SecurityProtectedExecutionReceipts
             (ExecutionReceiptId,RequestId,CorrelationId,Action,InitiatingShiftProfileId,
              ProposedEsdAdjustmentCanonical,ExecutedAtUtc,ResultStatus,ResultingConfigurationRevision)
            VALUES ($receipt,$request,$correlation,$action,$actor,$proposed,$at,'Succeeded',$revision);
            """;
        command.Parameters.AddWithValue("$receipt",value.ExecutionReceiptId!); command.Parameters.AddWithValue("$request",value.RequestId);
        command.Parameters.AddWithValue("$correlation",value.CorrelationId); command.Parameters.AddWithValue("$action",value.Action.ToString());
        command.Parameters.AddWithValue("$actor",value.InitiatingShiftProfileId); command.Parameters.AddWithValue("$proposed",proposed);
        command.Parameters.AddWithValue("$at",SQLiteShiftProfileRepository.Format(value.ConsumedAtUtc)); command.Parameters.AddWithValue("$revision",revision);
        await command.ExecuteNonQueryAsync(token);
    }
}
