using Microsoft.Data.Sqlite;
using Rah_Negar.Foundation.Application.Security;
using Rah_Negar.Infrastructure.Database;

namespace Rah_Negar.Infrastructure.Security;

/// <summary>
/// Inactive target recovery adapter. Credential replacement and its non-secret audit receipt share
/// one SQLite transaction, so a failed audit cannot retire the previous singleton credential.
/// </summary>
public sealed class SQLiteManagementCredentialRecoveryBoundary(ISqliteConnectionFactory connections)
    : IManagementCredentialRecoveryBoundary
{
    public async Task<bool> TryRotateAsync(ManagementCredentialRecord replacement,
        int expectedCurrentVersion, SecurityAuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        ArgumentNullException.ThrowIfNull(auditEvent);
        if (expectedCurrentVersion <= 0 || replacement.CredentialVersion <= expectedCurrentVersion ||
            !replacement.IsCurrent || !replacement.IsActive)
            return false;

        IReadOnlyDictionary<string, string> metadata = SecurityAuditMetadataBuilder.Create(
            auditEvent.NonSecretValueMetadata);
        await using SqliteConnection connection = await connections.OpenConnectionAsync(cancellationToken);
        await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using (SqliteCommand retire = connection.CreateCommand())
            {
                retire.Transaction = transaction;
                retire.CommandText = """
                    UPDATE SecurityManagementCredentials SET IsCurrent=0,IsActive=0,
                      RetiredAtUtc=$at,UpdatedAtUtc=$at
                    WHERE SingletonId=1 AND IsCurrent=1 AND CredentialVersion=$expected;
                    """;
                retire.Parameters.AddWithValue("$at", SQLiteShiftProfileRepository.Format(replacement.CreatedAtUtc));
                retire.Parameters.AddWithValue("$expected", expectedCurrentVersion);
                if (await retire.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return false;
                }
            }

            await using (SqliteCommand insert = connection.CreateCommand())
            {
                insert.Transaction = transaction;
                insert.CommandText = """
                    INSERT INTO SecurityManagementCredentials
                      (SingletonId,CredentialVersion,KdfAlgorithm,KdfParameters,Salt,PasswordVerifier,
                       IsCurrent,IsActive,CreatedAtUtc,UpdatedAtUtc,RetiredAtUtc)
                    VALUES (1,$version,$algorithm,$parameters,$salt,$verifier,1,1,$created,$updated,NULL);
                    """;
                insert.Parameters.AddWithValue("$version", replacement.CredentialVersion);
                insert.Parameters.AddWithValue("$algorithm", replacement.KdfAlgorithm);
                insert.Parameters.AddWithValue("$parameters", replacement.KdfParameters);
                insert.Parameters.AddWithValue("$salt", replacement.Salt);
                insert.Parameters.AddWithValue("$verifier", replacement.PasswordVerifier);
                insert.Parameters.AddWithValue("$created", SQLiteShiftProfileRepository.Format(replacement.CreatedAtUtc));
                insert.Parameters.AddWithValue("$updated", SQLiteShiftProfileRepository.Format(replacement.UpdatedAtUtc));
                await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await InsertAuditAsync(connection, transaction, auditEvent, metadata, cancellationToken)
                .ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (SqliteException ex) when (SQLiteShiftProfileCredentialRepository.IsExpectedRace(ex))
        {
            await SQLiteShiftProfileCredentialRepository.SafeRollbackAsync(transaction, cancellationToken);
            return false;
        }
        catch
        {
            await SQLiteShiftProfileCredentialRepository.SafeRollbackAsync(transaction, cancellationToken);
            return false;
        }
    }

    private static async Task InsertAuditAsync(SqliteConnection connection, SqliteTransaction transaction,
        SecurityAuditEvent auditEvent, IReadOnlyDictionary<string, string> metadata,
        CancellationToken cancellationToken)
    {
        string auditId = Guid.NewGuid().ToString("N");
        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO SecurityAuditEntries
                  (AuditEntryId,InitiatingShiftProfileId,Action,Scope,AuthorizationType,ResultCategory,
                   TimestampUtc,CorrelationId,RequestId)
                VALUES ($id,$actor,$action,$scope,$type,$result,$at,$correlation,NULL);
                """;
            command.Parameters.AddWithValue("$id", auditId);
            command.Parameters.AddWithValue("$actor", auditEvent.InitiatingShiftProfileId);
            command.Parameters.AddWithValue("$action", auditEvent.Action.ToString());
            command.Parameters.AddWithValue("$scope", auditEvent.Scope);
            command.Parameters.AddWithValue("$type", auditEvent.AuthorizationType.ToString());
            command.Parameters.AddWithValue("$result", auditEvent.Succeeded ? "Succeeded" : "Failed");
            command.Parameters.AddWithValue("$at", SQLiteShiftProfileRepository.Format(auditEvent.Timestamp));
            command.Parameters.AddWithValue("$correlation", auditEvent.CorrelationId);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        foreach ((string key, string value) in metadata)
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO SecurityAuditMetadata (AuditEntryId,MetadataKey,MetadataValue) VALUES ($id,$key,$value);";
            command.Parameters.AddWithValue("$id", auditId);
            command.Parameters.AddWithValue("$key", key);
            command.Parameters.AddWithValue("$value", value);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
