using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Rah_Negar.Foundation.Application.Security;
using Rah_Negar.Infrastructure.Database;

namespace Rah_Negar.Infrastructure.Security;

public sealed class SQLiteShiftProfileRepository(ISqliteConnectionFactory connections) : IShiftProfileRepository
{
    public async Task<IReadOnlyList<ShiftProfile>> ReadActiveAsync(string stationId, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await connections.OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectSql + " WHERE StationId=$station AND IsActive=1 ORDER BY ShiftNumber;";
        command.Parameters.AddWithValue("$station", stationId);
        return await ReadManyAsync(command, cancellationToken);
    }

    public async Task<ShiftProfile?> FindByPersonnelNoAsync(string stationId, string personnelNo, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await connections.OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectSql + " WHERE StationId=$station AND PersonnelNoNormalized=$personnel AND IsActive=1 LIMIT 1;";
        command.Parameters.AddWithValue("$station", stationId);
        command.Parameters.AddWithValue("$personnel", PersonnelNumberNormalizer.Normalize(personnelNo));
        IReadOnlyList<ShiftProfile> values = await ReadManyAsync(command, cancellationToken);
        return values.SingleOrDefault();
    }

    public async Task CreateAsync(ShiftProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        await using SqliteConnection connection = await connections.OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO SecurityShiftProfiles
              (ShiftProfileId,StationId,ShiftNumber,ShiftName,SupervisorFirstName,SupervisorLastName,
               PersonnelNo,PersonnelNoNormalized,IsActive,CreatedAtUtc,UpdatedAtUtc,Revision)
            VALUES ($id,$station,$number,$name,$first,$last,$personnel,$normalized,$active,$created,$updated,$revision);
            """;
        BindProfile(command, profile);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> UpdateAsync(ShiftProfile profile, long expectedRevision, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        await using SqliteConnection connection = await connections.OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            UPDATE SecurityShiftProfiles SET ShiftNumber=$number,ShiftName=$name,
              SupervisorFirstName=$first,SupervisorLastName=$last,PersonnelNo=$personnel,
              PersonnelNoNormalized=$normalized,IsActive=$active,UpdatedAtUtc=$updated,Revision=$next
            WHERE ShiftProfileId=$id AND StationId=$station AND Revision=$expected;
            """;
        BindProfile(command, profile);
        command.Parameters.AddWithValue("$expected", expectedRevision);
        command.Parameters.AddWithValue("$next", checked(expectedRevision + 1));
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private const string SelectSql = """
        SELECT ShiftProfileId,StationId,ShiftNumber,ShiftName,SupervisorFirstName,SupervisorLastName,
               PersonnelNo,IsActive,CreatedAtUtc,UpdatedAtUtc,Revision FROM SecurityShiftProfiles
        """;

    private static async Task<IReadOnlyList<ShiftProfile>> ReadManyAsync(SqliteCommand command, CancellationToken token)
    {
        var values = new List<ShiftProfile>();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
            values.Add(new(reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetString(3),
                reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetInt32(7) == 1,
                Parse(reader.GetString(8)), Parse(reader.GetString(9)), reader.GetInt64(10)));
        return values;
    }

    private static void BindProfile(SqliteCommand command, ShiftProfile profile)
    {
        command.Parameters.AddWithValue("$id", profile.ShiftProfileId);
        command.Parameters.AddWithValue("$station", profile.StationId);
        command.Parameters.AddWithValue("$number", profile.ShiftNumber);
        command.Parameters.AddWithValue("$name", profile.ShiftName);
        command.Parameters.AddWithValue("$first", profile.SupervisorFirstName);
        command.Parameters.AddWithValue("$last", profile.SupervisorLastName);
        command.Parameters.AddWithValue("$personnel", profile.PersonnelNo);
        command.Parameters.AddWithValue("$normalized", PersonnelNumberNormalizer.Normalize(profile.PersonnelNo));
        command.Parameters.AddWithValue("$active", profile.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("$created", Format(profile.CreatedAt));
        command.Parameters.AddWithValue("$updated", Format(profile.UpdatedAt));
        command.Parameters.AddWithValue("$revision", profile.Revision);
    }

    internal static string Format(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    internal static DateTimeOffset Parse(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
}

public sealed class SQLiteShiftProfileCredentialRepository(ISqliteConnectionFactory connections) : IShiftProfileCredentialRepository
{
    public async Task<ShiftProfileCredentialRecord?> LoadCurrentAsync(string shiftProfileId, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await connections.OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT ShiftProfileId,CredentialVersion,KdfAlgorithm,KdfParameters,Salt,PasswordVerifier,
                   IsCurrent,CreatedAtUtc,RetiredAtUtc FROM SecurityShiftProfileCredentials
            WHERE ShiftProfileId=$id AND IsCurrent=1;
            """;
        command.Parameters.AddWithValue("$id", shiftProfileId);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    public async Task<bool> ReplaceAsync(ShiftProfileCredentialRecord replacement, int? expectedCurrentVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        await using SqliteConnection connection = await connections.OpenConnectionAsync(cancellationToken);
        await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            int retired = await RetireAsync(connection, transaction, replacement.ShiftProfileId,
                expectedCurrentVersion, replacement.CreatedAtUtc, cancellationToken);
            if (expectedCurrentVersion.HasValue && retired != 1) { await transaction.RollbackAsync(cancellationToken); return false; }
            if (!expectedCurrentVersion.HasValue && retired != 0) { await transaction.RollbackAsync(cancellationToken); return false; }
            await InsertAsync(connection, transaction, replacement with { IsCurrent = true, RetiredAtUtc = null }, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (SqliteException ex) when (IsExpectedRace(ex)) { await SafeRollbackAsync(transaction, cancellationToken); return false; }
    }

    private static async Task<int> RetireAsync(SqliteConnection connection, SqliteTransaction transaction,
        string id, int? expected, DateTimeOffset at, CancellationToken token)
    {
        await using SqliteCommand command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            UPDATE SecurityShiftProfileCredentials SET IsCurrent=0,RetiredAtUtc=$at
            WHERE ShiftProfileId=$id AND IsCurrent=1 AND CredentialVersion=$expected;
            """;
        command.Parameters.AddWithValue("$at", SQLiteShiftProfileRepository.Format(at));
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$expected", expected ?? -1);
        return await command.ExecuteNonQueryAsync(token);
    }

    private static async Task InsertAsync(SqliteConnection connection, SqliteTransaction transaction,
        ShiftProfileCredentialRecord value, CancellationToken token)
    {
        await using SqliteCommand command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO SecurityShiftProfileCredentials
             (ShiftProfileId,CredentialVersion,KdfAlgorithm,KdfParameters,Salt,PasswordVerifier,IsCurrent,CreatedAtUtc,RetiredAtUtc)
            VALUES ($id,$version,$algorithm,$parameters,$salt,$verifier,1,$created,NULL);
            """;
        command.Parameters.AddWithValue("$id", value.ShiftProfileId);
        command.Parameters.AddWithValue("$version", value.CredentialVersion);
        command.Parameters.AddWithValue("$algorithm", value.KdfAlgorithm);
        command.Parameters.AddWithValue("$parameters", value.KdfParameters);
        command.Parameters.AddWithValue("$salt", value.Salt);
        command.Parameters.AddWithValue("$verifier", value.PasswordVerifier);
        command.Parameters.AddWithValue("$created", SQLiteShiftProfileRepository.Format(value.CreatedAtUtc));
        await command.ExecuteNonQueryAsync(token);
    }

    private static ShiftProfileCredentialRecord Read(SqliteDataReader r) => new(r.GetString(0), r.GetInt32(1),
        r.GetString(2), r.GetString(3), (byte[])r[4], (byte[])r[5], r.GetInt32(6) == 1,
        SQLiteShiftProfileRepository.Parse(r.GetString(7)), r.IsDBNull(8) ? null : SQLiteShiftProfileRepository.Parse(r.GetString(8)));

    internal static bool IsExpectedRace(SqliteException ex) => ex.SqliteErrorCode is 5 or 6 or 19;
    internal static async Task SafeRollbackAsync(SqliteTransaction transaction, CancellationToken token)
    { try { await transaction.RollbackAsync(token); } catch { } }
}

public sealed class SQLiteManagementCredentialRepository(ISqliteConnectionFactory connections) : IManagementCredentialRepository
{
    public async Task<ManagementCredentialRecord?> LoadCurrentAsync(CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await connections.OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT CredentialVersion,KdfAlgorithm,KdfParameters,Salt,PasswordVerifier,IsCurrent,IsActive,
                   CreatedAtUtc,UpdatedAtUtc,RetiredAtUtc FROM SecurityManagementCredentials
            WHERE SingletonId=1 AND IsCurrent=1;
            """;
        await using SqliteDataReader r = await command.ExecuteReaderAsync(cancellationToken);
        return await r.ReadAsync(cancellationToken) ? new(r.GetInt32(0),r.GetString(1),r.GetString(2),(byte[])r[3],(byte[])r[4],
            r.GetInt32(5)==1,r.GetInt32(6)==1,SQLiteShiftProfileRepository.Parse(r.GetString(7)),
            SQLiteShiftProfileRepository.Parse(r.GetString(8)),r.IsDBNull(9)?null:SQLiteShiftProfileRepository.Parse(r.GetString(9))) : null;
    }

    public async Task<bool> ReplaceAsync(ManagementCredentialRecord replacement, int? expectedCurrentVersion,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await connections.OpenConnectionAsync(cancellationToken);
        await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using (SqliteCommand retire = connection.CreateCommand())
            {
                retire.Transaction = transaction;
                retire.CommandText = """
                    UPDATE SecurityManagementCredentials SET IsCurrent=0,IsActive=0,RetiredAtUtc=$at,UpdatedAtUtc=$at
                    WHERE SingletonId=1 AND IsCurrent=1 AND CredentialVersion=$expected;
                    """;
                retire.Parameters.AddWithValue("$at", SQLiteShiftProfileRepository.Format(replacement.CreatedAtUtc));
                retire.Parameters.AddWithValue("$expected", expectedCurrentVersion ?? -1);
                int count = await retire.ExecuteNonQueryAsync(cancellationToken);
                if ((expectedCurrentVersion.HasValue && count != 1) || (!expectedCurrentVersion.HasValue && count != 0))
                { await transaction.RollbackAsync(cancellationToken); return false; }
            }
            await using SqliteCommand insert = connection.CreateCommand(); insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO SecurityManagementCredentials
                 (SingletonId,CredentialVersion,KdfAlgorithm,KdfParameters,Salt,PasswordVerifier,IsCurrent,IsActive,CreatedAtUtc,UpdatedAtUtc,RetiredAtUtc)
                VALUES (1,$version,$algorithm,$parameters,$salt,$verifier,1,$active,$created,$updated,NULL);
                """;
            insert.Parameters.AddWithValue("$version", replacement.CredentialVersion);
            insert.Parameters.AddWithValue("$algorithm", replacement.KdfAlgorithm);
            insert.Parameters.AddWithValue("$parameters", replacement.KdfParameters);
            insert.Parameters.AddWithValue("$salt", replacement.Salt);
            insert.Parameters.AddWithValue("$verifier", replacement.PasswordVerifier);
            insert.Parameters.AddWithValue("$active", replacement.IsActive ? 1 : 0);
            insert.Parameters.AddWithValue("$created", SQLiteShiftProfileRepository.Format(replacement.CreatedAtUtc));
            insert.Parameters.AddWithValue("$updated", SQLiteShiftProfileRepository.Format(replacement.UpdatedAtUtc));
            await insert.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken); return true;
        }
        catch (SqliteException ex) when (SQLiteShiftProfileCredentialRepository.IsExpectedRace(ex))
        { await SQLiteShiftProfileCredentialRepository.SafeRollbackAsync(transaction, cancellationToken); return false; }
    }
}

public sealed class SQLiteDeviceIdentityRepository(ISqliteConnectionFactory connections) : IDeviceIdentityRepository
{
    public async Task<DeviceIdentityRecord?> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await connections.OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT DeviceId,ProvisionedAtUtc,Revision FROM SecurityDeviceIdentity WHERE SingletonId=1;";
        await using SqliteDataReader r = await command.ExecuteReaderAsync(cancellationToken);
        return await r.ReadAsync(cancellationToken) ? new(r.GetString(0),SQLiteShiftProfileRepository.Parse(r.GetString(1)),r.GetInt64(2)) : null;
    }
    public async Task<string> GetDeviceIdAsync(CancellationToken cancellationToken = default) =>
        (await LoadAsync(cancellationToken))?.DeviceId ?? throw new InvalidOperationException("Device identity is not provisioned.");
    public async Task<bool> TryProvisionAsync(DeviceIdentityRecord identity, CancellationToken cancellationToken = default)
    {
        try
        {
            await using SqliteConnection connection = await connections.OpenConnectionAsync(cancellationToken);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "INSERT INTO SecurityDeviceIdentity (SingletonId,DeviceId,ProvisionedAtUtc,Revision) VALUES (1,$id,$at,$revision);";
            command.Parameters.AddWithValue("$id", identity.DeviceId);
            command.Parameters.AddWithValue("$at", SQLiteShiftProfileRepository.Format(identity.ProvisionedAtUtc));
            command.Parameters.AddWithValue("$revision", identity.Revision);
            return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
        }
        catch (SqliteException ex) when (SQLiteShiftProfileCredentialRepository.IsExpectedRace(ex)) { return false; }
    }
}

public sealed class SQLiteTrustedVendorPublicKeyRepository(ISqliteConnectionFactory connections) : ITrustedVendorPublicKeyProvider
{
    public async Task<bool> AddAsync(TrustedVendorPublicKey key, string algorithm, DateTimeOffset createdAtUtc,
        long revision, CancellationToken cancellationToken = default)
    {
        try
        {
            await using SqliteConnection connection = await connections.OpenConnectionAsync(cancellationToken);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO SecurityTrustedVendorPublicKeys
                 (KeyId,PublicVerificationMaterial,Algorithm,ActivatedAtUtc,RetiredAtUtc,CreatedAtUtc,Revision,MaterialSha256)
                VALUES ($id,$material,$algorithm,$activated,$retired,$created,$revision,$sha);
                """;
            byte[] material = key.SubjectPublicKeyInfo.ToArray();
            command.Parameters.AddWithValue("$id", key.KeyId); command.Parameters.AddWithValue("$material", material);
            command.Parameters.AddWithValue("$algorithm", algorithm);
            command.Parameters.AddWithValue("$activated", SQLiteShiftProfileRepository.Format(key.ActivatedAtUtc));
            command.Parameters.AddWithValue("$retired", key.RetiredAtUtc is null ? DBNull.Value : SQLiteShiftProfileRepository.Format(key.RetiredAtUtc.Value));
            command.Parameters.AddWithValue("$created", SQLiteShiftProfileRepository.Format(createdAtUtc));
            command.Parameters.AddWithValue("$revision", revision);
            command.Parameters.AddWithValue("$sha", Convert.ToHexString(SHA256.HashData(material)));
            return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
        }
        catch (SqliteException ex) when (SQLiteShiftProfileCredentialRepository.IsExpectedRace(ex)) { return false; }
    }

    public async Task<TrustedVendorPublicKey?> FindByKeyIdAsync(string keyId, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await connections.OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT KeyId,PublicVerificationMaterial,ActivatedAtUtc,RetiredAtUtc FROM SecurityTrustedVendorPublicKeys WHERE KeyId=$id;";
        command.Parameters.AddWithValue("$id", keyId);
        await using SqliteDataReader r = await command.ExecuteReaderAsync(cancellationToken);
        return await r.ReadAsync(cancellationToken) ? new(r.GetString(0),(byte[])r[1],SQLiteShiftProfileRepository.Parse(r.GetString(2)),
            r.IsDBNull(3)?null:SQLiteShiftProfileRepository.Parse(r.GetString(3))) : null;
    }

    public async Task<bool> RetireAsync(string keyId, DateTimeOffset retiredAtUtc, long expectedRevision,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await connections.OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            UPDATE SecurityTrustedVendorPublicKeys SET RetiredAtUtc=$retired,Revision=Revision+1
            WHERE KeyId=$id AND Revision=$expected AND RetiredAtUtc IS NULL AND ActivatedAtUtc<$retired;
            """;
        command.Parameters.AddWithValue("$retired", SQLiteShiftProfileRepository.Format(retiredAtUtc));
        command.Parameters.AddWithValue("$id", keyId); command.Parameters.AddWithValue("$expected", expectedRevision);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }
}

public sealed class SQLiteSecurityAuditSink(ISqliteConnectionFactory connections) : ISecurityAuditSink
{
    public async Task WriteAsync(SecurityAuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<string,string> safe = SecurityAuditMetadataBuilder.Create(auditEvent.NonSecretValueMetadata);
        await using SqliteConnection connection = await connections.OpenConnectionAsync(cancellationToken);
        await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.Transaction=transaction; command.CommandText="""
                INSERT INTO SecurityAuditEntries
                 (AuditEntryId,InitiatingShiftProfileId,Action,Scope,AuthorizationType,ResultCategory,TimestampUtc,CorrelationId,RequestId)
                VALUES ($id,$actor,$action,$scope,$type,$result,$at,$correlation,$request);
                """;
            string id=Guid.NewGuid().ToString("N");
            command.Parameters.AddWithValue("$id",id); command.Parameters.AddWithValue("$actor",auditEvent.InitiatingShiftProfileId);
            command.Parameters.AddWithValue("$action",auditEvent.Action.ToString()); command.Parameters.AddWithValue("$scope",auditEvent.Scope);
            command.Parameters.AddWithValue("$type",auditEvent.AuthorizationType.ToString());
            command.Parameters.AddWithValue("$result",auditEvent.Succeeded?"Succeeded":"Failed");
            command.Parameters.AddWithValue("$at",SQLiteShiftProfileRepository.Format(auditEvent.Timestamp));
            command.Parameters.AddWithValue("$correlation",auditEvent.CorrelationId);
            command.Parameters.AddWithValue("$request",safe.TryGetValue("RequestId",out string? request)?request:DBNull.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
            foreach ((string key,string value) in safe)
            {
                await using SqliteCommand metadata=connection.CreateCommand(); metadata.Transaction=transaction;
                metadata.CommandText="INSERT INTO SecurityAuditMetadata (AuditEntryId,MetadataKey,MetadataValue) VALUES ($id,$key,$value);";
                metadata.Parameters.AddWithValue("$id",id); metadata.Parameters.AddWithValue("$key",key); metadata.Parameters.AddWithValue("$value",value);
                await metadata.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        await transaction.CommitAsync(cancellationToken);
    }
}

/// <summary>
/// Durable fail-closed replay reservation. Successful ESD execution uses the atomic adapter instead;
/// a standalone reservation is immutable and intentionally cannot later authorize execution.
/// </summary>
public sealed class SQLiteConsumedVendorAuthorizationStore(ISqliteConnectionFactory connections)
    : IConsumedVendorAuthorizationStore
{
    public async Task<bool> IsConsumedAsync(string requestId, string correlationId,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await connections.OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS(SELECT 1 FROM SecurityConsumedVendorAuthorizations
              WHERE RequestId=$request AND CorrelationId=$correlation);
            """;
        command.Parameters.AddWithValue("$request", requestId);
        command.Parameters.AddWithValue("$correlation", correlationId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
    }

    public async Task<bool> TryConsumeAsync(VendorAuthorizationConsumption value,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(value.RequestId) || string.IsNullOrWhiteSpace(value.CorrelationId) ||
            string.IsNullOrWhiteSpace(value.ExecutionReceiptId) || string.IsNullOrWhiteSpace(value.DeviceId) ||
            string.IsNullOrWhiteSpace(value.KeyId) || string.IsNullOrWhiteSpace(value.InitiatingShiftProfileId) ||
            value.Action != VendorSupportAction.ChangeEsdAdjustment) return false;
        try
        {
            await using SqliteConnection connection = await connections.OpenConnectionAsync(cancellationToken);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO SecurityConsumedVendorAuthorizations
                 (RequestId,CorrelationId,DeviceId,Action,ProposedEsdAdjustmentCanonical,KeyId,ConsumedAtUtc,
                  InitiatingShiftProfileId,ExecutionReceiptId,ResultStatus)
                VALUES ($request,$correlation,$device,'ChangeEsdAdjustment',$proposed,$key,$at,$actor,$receipt,'Consumed');
                """;
            command.Parameters.AddWithValue("$request",value.RequestId); command.Parameters.AddWithValue("$correlation",value.CorrelationId);
            command.Parameters.AddWithValue("$device",value.DeviceId);
            command.Parameters.AddWithValue("$proposed",value.ProposedEsdAdjustment.ToString("G29",CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$key",value.KeyId); command.Parameters.AddWithValue("$at",SQLiteShiftProfileRepository.Format(value.ConsumedAtUtc));
            command.Parameters.AddWithValue("$actor",value.InitiatingShiftProfileId); command.Parameters.AddWithValue("$receipt",value.ExecutionReceiptId);
            return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
        }
        catch (SqliteException ex) when (SQLiteShiftProfileCredentialRepository.IsExpectedRace(ex)) { return false; }
    }
}
