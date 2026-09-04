using System.Globalization;
using Microsoft.Data.Sqlite;
using Rah_Negar.Foundation.Application.Provisioning;
using Rah_Negar.Foundation.Application.Security;
using Rah_Negar.Infrastructure.Security;

namespace Rah_Negar.Infrastructure.Database.Provisioning;

/// <summary>
/// Explicit-path, transactional target preparation boundary. It only prepares a caller-selected
/// target database; it never discovers a path, changes routing, or mutates the legacy database.
/// </summary>
public sealed class SQLiteTargetStationProvisioningBoundary(ISqliteConnectionFactory connections)
    : ITargetStationProvisioningBoundary
{
    private static readonly string[] RequiredTables =
    [
        "Stations", "Units", "SecurityShiftProfiles", "SecurityShiftProfileCredentials",
        "SecurityManagementCredentials", "SecurityDeviceIdentity", "SecurityTrustedVendorPublicKeys",
        "SecurityDeploymentSettings", "Events", "ReportSnapshots", "ReportPeriodLocks"
    ];

    public async Task<TargetProvisioningResult> ProvisionAsync(
        TargetStationProvisioningPackage package,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        TargetProvisioningValidationResult validation = TargetStationProvisioningManifestBuilder.Validate(package);
        if (!validation.IsValid)
            return Rejected(TargetProvisioningFailure.InvalidManifest, validation.Issues);

        TargetStationProvisioningManifest manifest = validation.Manifest!;
        try
        {
            await using SqliteConnection connection = await connections.OpenConnectionAsync(cancellationToken)
                .ConfigureAwait(false);
            await using SqliteTransaction transaction = (SqliteTransaction)await connection
                .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            if (!await HasRequiredSchemaAsync(connection, transaction, cancellationToken).ConfigureAwait(false))
                return Rejected(TargetProvisioningFailure.SchemaUnavailable, ["target-schema-required"]);

            bool hadData = await HasStationDataBeforeProvisioningAsync(connection, transaction, package.StationId,
                package, cancellationToken).ConfigureAwait(false);
            await InsertStationAsync(connection, transaction, package, cancellationToken).ConfigureAwait(false);
            foreach (TargetUnitProvisioningRecord unit in package.Units)
                await InsertUnitAsync(connection, transaction, unit, cancellationToken).ConfigureAwait(false);
            foreach (TargetShiftProfileProvisioningRecord profile in package.ShiftProfiles)
            {
                await InsertProfileAsync(connection, transaction, profile.Profile, cancellationToken).ConfigureAwait(false);
                await InsertShiftCredentialAsync(connection, transaction, profile.Credential, cancellationToken).ConfigureAwait(false);
            }
            await InsertManagementCredentialAsync(connection, transaction, package.ManagementCredential, cancellationToken)
                .ConfigureAwait(false);
            await InsertDeviceAsync(connection, transaction, package.DeviceIdentity, cancellationToken).ConfigureAwait(false);
            await InsertVendorKeyAsync(connection, transaction, package.TrustedVendorKey, cancellationToken).ConfigureAwait(false);
            await InsertEsdAsync(connection, transaction, package.EsdAdjustmentCanonical, cancellationToken).ConfigureAwait(false);
            foreach (TargetEventProvisioningRecord item in package.Events)
                await InsertEventAsync(connection, transaction, item, cancellationToken).ConfigureAwait(false);
            foreach (TargetFinalizedSnapshotProvisioningRecord item in package.FinalizedSnapshots
                .OrderBy(x => x.SnapshotSequence).ThenBy(x => x.SnapshotId, StringComparer.Ordinal))
                await InsertSnapshotAsync(connection, transaction, item, cancellationToken).ConfigureAwait(false);
            foreach (TargetFinalizedLockProvisioningRecord item in package.FinalizedLocks)
                await InsertLockAsync(connection, transaction, item, cancellationToken).ConfigureAwait(false);

            IReadOnlyList<string> conflicts = await VerifyAsync(connection, transaction, package, cancellationToken)
                .ConfigureAwait(false);
            if (conflicts.Count != 0)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Rejected(TargetProvisioningFailure.Conflict, conflicts);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(hadData ? TargetProvisioningOutcome.AlreadyProvisioned : TargetProvisioningOutcome.Provisioned,
                TargetProvisioningFailure.None, manifest, []);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (SqliteException)
        {
            return Rejected(TargetProvisioningFailure.InfrastructureFailure, ["sqlite-provisioning-failed"]);
        }
        catch
        {
            return Rejected(TargetProvisioningFailure.InfrastructureFailure, ["provisioning-failed"]);
        }
    }

    private static async Task<bool> HasRequiredSchemaAsync(SqliteConnection c, SqliteTransaction t, CancellationToken token)
    {
        await using SqliteCommand command = c.CreateCommand();
        command.Transaction = t;
        command.CommandText = "SELECT name FROM sqlite_schema WHERE type='table' AND name NOT LIKE 'sqlite_%';";
        var names = new HashSet<string>(StringComparer.Ordinal);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        while (await reader.ReadAsync(token).ConfigureAwait(false)) names.Add(reader.GetString(0));
        return RequiredTables.All(names.Contains);
    }

    private static async Task InsertStationAsync(SqliteConnection c, SqliteTransaction t,
        TargetStationProvisioningPackage p, CancellationToken token) =>
        await ExecuteAsync(c, t, """
            INSERT OR IGNORE INTO Stations (StationId,StationName,CreatedAtUtc,Revision)
            VALUES ($id,$name,$created,1);
            """, token, ("$id", p.StationId), ("$name", p.StationName),
            ("$created", Format(p.CreatedAtUtc))).ConfigureAwait(false);

    private static async Task InsertUnitAsync(SqliteConnection c, SqliteTransaction t,
        TargetUnitProvisioningRecord x, CancellationToken token) =>
        await ExecuteAsync(c, t, """
            INSERT OR IGNORE INTO Units
              (StationId,UnitId,UnitNumber,UnitName,IsActive,Revision)
            VALUES ($station,$id,$number,$name,$active,$revision);
            """, token, ("$station", x.StationId), ("$id", x.UnitId), ("$number", x.UnitNumber),
            ("$name", x.UnitName), ("$active", x.IsActive ? 1 : 0), ("$revision", x.Revision)).ConfigureAwait(false);

    private static async Task InsertProfileAsync(SqliteConnection c, SqliteTransaction t,
        ShiftProfile x, CancellationToken token) =>
        await ExecuteAsync(c, t, """
            INSERT OR IGNORE INTO SecurityShiftProfiles
              (ShiftProfileId,StationId,ShiftNumber,ShiftName,SupervisorFirstName,SupervisorLastName,
               PersonnelNo,PersonnelNoNormalized,IsActive,CreatedAtUtc,UpdatedAtUtc,Revision)
            VALUES ($id,$station,$number,$name,$first,$last,$personnel,$normalized,$active,$created,$updated,$revision);
            """, token, ("$id", x.ShiftProfileId), ("$station", x.StationId), ("$number", x.ShiftNumber),
            ("$name", x.ShiftName), ("$first", x.SupervisorFirstName), ("$last", x.SupervisorLastName),
            ("$personnel", x.PersonnelNo), ("$normalized", PersonnelNumberNormalizer.Normalize(x.PersonnelNo)),
            ("$active", x.IsActive ? 1 : 0), ("$created", Format(x.CreatedAt)), ("$updated", Format(x.UpdatedAt)),
            ("$revision", x.Revision)).ConfigureAwait(false);

    private static async Task InsertShiftCredentialAsync(SqliteConnection c, SqliteTransaction t,
        ShiftProfileCredentialRecord x, CancellationToken token) =>
        await ExecuteAsync(c, t, """
            INSERT OR IGNORE INTO SecurityShiftProfileCredentials
              (ShiftProfileId,CredentialVersion,KdfAlgorithm,KdfParameters,Salt,PasswordVerifier,
               IsCurrent,CreatedAtUtc,RetiredAtUtc)
            VALUES ($id,$version,$algorithm,$parameters,$salt,$verifier,$current,$created,$retired);
            """, token, ("$id", x.ShiftProfileId), ("$version", x.CredentialVersion),
            ("$algorithm", x.KdfAlgorithm), ("$parameters", x.KdfParameters), ("$salt", x.Salt),
            ("$verifier", x.PasswordVerifier), ("$current", x.IsCurrent ? 1 : 0),
            ("$created", Format(x.CreatedAtUtc)), ("$retired", x.RetiredAtUtc is null ? DBNull.Value : Format(x.RetiredAtUtc.Value)))
            .ConfigureAwait(false);

    private static async Task InsertManagementCredentialAsync(SqliteConnection c, SqliteTransaction t,
        ManagementCredentialRecord x, CancellationToken token) =>
        await ExecuteAsync(c, t, """
            INSERT OR IGNORE INTO SecurityManagementCredentials
              (SingletonId,CredentialVersion,KdfAlgorithm,KdfParameters,Salt,PasswordVerifier,
               IsCurrent,IsActive,CreatedAtUtc,UpdatedAtUtc,RetiredAtUtc)
            VALUES (1,$version,$algorithm,$parameters,$salt,$verifier,$current,$active,$created,$updated,$retired);
            """, token, ("$version", x.CredentialVersion), ("$algorithm", x.KdfAlgorithm),
            ("$parameters", x.KdfParameters), ("$salt", x.Salt), ("$verifier", x.PasswordVerifier),
            ("$current", x.IsCurrent ? 1 : 0), ("$active", x.IsActive ? 1 : 0), ("$created", Format(x.CreatedAtUtc)),
            ("$updated", Format(x.UpdatedAtUtc)), ("$retired", x.RetiredAtUtc is null ? DBNull.Value : Format(x.RetiredAtUtc.Value)))
            .ConfigureAwait(false);

    private static async Task InsertDeviceAsync(SqliteConnection c, SqliteTransaction t,
        DeviceIdentityRecord x, CancellationToken token) =>
        await ExecuteAsync(c, t, """
            INSERT OR IGNORE INTO SecurityDeviceIdentity
              (SingletonId,DeviceId,ProvisionedAtUtc,Revision)
            VALUES (1,$id,$at,$revision);
            """, token, ("$id", x.DeviceId), ("$at", Format(x.ProvisionedAtUtc)), ("$revision", x.Revision))
            .ConfigureAwait(false);

    private static async Task InsertVendorKeyAsync(SqliteConnection c, SqliteTransaction t,
        TargetTrustedVendorKeyProvisioningRecord x, CancellationToken token) =>
        await ExecuteAsync(c, t, """
            INSERT OR IGNORE INTO SecurityTrustedVendorPublicKeys
              (KeyId,PublicVerificationMaterial,Algorithm,ActivatedAtUtc,RetiredAtUtc,CreatedAtUtc,Revision,MaterialSha256)
            VALUES ($id,$material,$algorithm,$activated,$retired,$created,$revision,$sha);
            """, token, ("$id", x.KeyId), ("$material", x.SubjectPublicKeyInfo), ("$algorithm", x.Algorithm),
            ("$activated", Format(x.ActivatedAtUtc)), ("$retired", x.RetiredAtUtc is null ? DBNull.Value : Format(x.RetiredAtUtc.Value)),
            ("$created", Format(x.CreatedAtUtc)), ("$revision", x.Revision),
            ("$sha", Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(x.SubjectPublicKeyInfo))))
            .ConfigureAwait(false);

    private static async Task InsertEsdAsync(SqliteConnection c, SqliteTransaction t,
        string value, CancellationToken token) =>
        await ExecuteAsync(c, t, """
            INSERT OR IGNORE INTO SecurityDeploymentSettings
              (SingletonId,EsdAdjustmentCanonical,Revision,UpdatedAtUtc,UpdatedByShiftProfileId)
            VALUES (1,$value,1,$at,NULL);
            """, token, ("$value", value), ("$at", Format(DateTimeOffset.UtcNow))).ConfigureAwait(false);

    private static async Task InsertEventAsync(SqliteConnection c, SqliteTransaction t,
        TargetEventProvisioningRecord x, CancellationToken token) =>
        await ExecuteAsync(c, t, """
            INSERT OR IGNORE INTO Events
              (EventId,StationId,UnitId,EventType,EventDate,EventTime,EventDateTime,Remark,
               CreatedAt,CreatedByShiftProfileId,UpdatedAt,IsDeleted,DeletedAt,DeletedByShiftProfileId,RowVersion)
            VALUES ($id,$station,$unit,$type,$date,$time,$datetime,$remark,$created,$actor,NULL,0,NULL,NULL,$version);
            """, token, ("$id", x.EventId), ("$station", x.StationId), ("$unit", x.UnitId),
            ("$type", x.EventType), ("$date", x.EventDate), ("$time", x.EventTime), ("$datetime", x.EventDateTime),
            ("$remark", x.Remark is null ? DBNull.Value : x.Remark), ("$created", Format(x.CreatedAtUtc)),
            ("$actor", x.CreatedByShiftProfileId), ("$version", x.RowVersion)).ConfigureAwait(false);

    private static async Task InsertSnapshotAsync(SqliteConnection c, SqliteTransaction t,
        TargetFinalizedSnapshotProvisioningRecord x, CancellationToken token) =>
        await ExecuteAsync(c, t, """
            INSERT OR IGNORE INTO ReportSnapshots
              (SnapshotId,ReportId,StationId,PeriodStartMinute,PeriodEndMinute,PeriodKind,SnapshotSequence,
               SupersedesSnapshotId,PayloadSchemaVersion,CanonicalJson,ChecksumAlgorithm,IntegrityFormatVersion,
               ChecksumValue,CanonicalPayloadLength,SourceRevision,FinalizedAt)
            VALUES ($id,$report,$station,$start,$end,$kind,$sequence,$supersedes,$payload,$json,$algorithm,$format,
               $checksum,$length,$source,$finalized);
            """, token, ("$id", x.SnapshotId), ("$report", x.ReportId), ("$station", x.StationId),
            ("$start", x.PeriodStartMinute), ("$end", x.PeriodEndMinute), ("$kind", x.PeriodKind),
            ("$sequence", x.SnapshotSequence), ("$supersedes", x.SupersedesSnapshotId is null ? DBNull.Value : x.SupersedesSnapshotId),
            ("$payload", x.PayloadSchemaVersion), ("$json", x.CanonicalJson), ("$algorithm", x.ChecksumAlgorithm),
            ("$format", x.IntegrityFormatVersion), ("$checksum", x.ChecksumValue), ("$length", x.CanonicalPayloadLength),
            ("$source", x.SourceRevision), ("$finalized", Format(x.FinalizedAt))).ConfigureAwait(false);

    private static async Task InsertLockAsync(SqliteConnection c, SqliteTransaction t,
        TargetFinalizedLockProvisioningRecord x, CancellationToken token) =>
        await ExecuteAsync(c, t, """
            INSERT OR IGNORE INTO ReportPeriodLocks
              (StationId,PeriodStartMinute,PeriodEndMinute,PeriodKind,LockState,EffectiveSnapshotId,
               Revision,FinalizationId,FinalizedAt,ActorIdentity)
            VALUES ($station,$start,$end,$kind,'Finalized',$snapshot,$revision,$finalization,$at,$actor);
            """, token, ("$station", x.StationId), ("$start", x.PeriodStartMinute), ("$end", x.PeriodEndMinute),
            ("$kind", x.PeriodKind), ("$snapshot", x.EffectiveSnapshotId), ("$revision", x.Revision),
            ("$finalization", x.FinalizationId), ("$at", Format(x.FinalizedAt)), ("$actor", x.ActorIdentity))
            .ConfigureAwait(false);

    private static async Task<IReadOnlyList<string>> VerifyAsync(SqliteConnection c, SqliteTransaction t,
        TargetStationProvisioningPackage p, CancellationToken token)
    {
        var issues = new List<string>();
        if (!await ExistsAsync(c, t, "SELECT 1 FROM Stations WHERE StationId=$id AND StationName=$name AND Revision=1;",
                token, ("$id", p.StationId), ("$name", p.StationName)).ConfigureAwait(false))
            issues.Add("station-mapping-conflict");
        if (await CountAsync(c, t, "SELECT COUNT(*) FROM Units WHERE StationId=$station;", token, ("$station", p.StationId))
            .ConfigureAwait(false) != p.Units.Count)
            issues.Add("unit-mapping-conflict");
        if (await CountAsync(c, t, "SELECT COUNT(*) FROM Units WHERE StationId<>$station;", token, ("$station", p.StationId))
            .ConfigureAwait(false) != 0)
            issues.Add("cross-station-unit-contamination");
        foreach (TargetUnitProvisioningRecord unit in p.Units)
            if (!await ExistsAsync(c, t, """
                SELECT 1 FROM Units
                 WHERE StationId=$station AND UnitId=$id AND UnitNumber=$number AND UnitName=$name
                   AND IsActive=$active AND Revision=$revision;
                """, token, ("$station", unit.StationId), ("$id", unit.UnitId), ("$number", unit.UnitNumber),
                ("$name", unit.UnitName), ("$active", unit.IsActive ? 1 : 0), ("$revision", unit.Revision))
                .ConfigureAwait(false))
                issues.Add("unit-record-conflict");
        if (await CountAsync(c, t, "SELECT COUNT(*) FROM SecurityShiftProfiles WHERE StationId=$station AND IsActive=1;",
                token, ("$station", p.StationId)).ConfigureAwait(false) != p.ShiftProfiles.Count)
            issues.Add("shift-profile-mapping-conflict");
        if (await CountAsync(c, t, "SELECT COUNT(*) FROM SecurityShiftProfiles WHERE StationId<>$station;", token,
                ("$station", p.StationId)).ConfigureAwait(false) != 0)
            issues.Add("cross-station-shift-profile-contamination");
        foreach (TargetShiftProfileProvisioningRecord item in p.ShiftProfiles)
        {
            ShiftProfile profile = item.Profile;
            if (!await ExistsAsync(c, t, """
                SELECT 1 FROM SecurityShiftProfiles
                 WHERE ShiftProfileId=$id AND StationId=$station AND ShiftNumber=$number AND ShiftName=$name
                   AND SupervisorFirstName=$first AND SupervisorLastName=$last AND PersonnelNo=$personnel
                   AND PersonnelNoNormalized=$normalized AND IsActive=$active AND Revision=$revision;
                """, token, ("$id", profile.ShiftProfileId), ("$station", profile.StationId),
                ("$number", profile.ShiftNumber), ("$name", profile.ShiftName), ("$first", profile.SupervisorFirstName),
                ("$last", profile.SupervisorLastName), ("$personnel", profile.PersonnelNo),
                ("$normalized", PersonnelNumberNormalizer.Normalize(profile.PersonnelNo)),
                ("$active", profile.IsActive ? 1 : 0), ("$revision", profile.Revision)).ConfigureAwait(false) ||
                !await ExistsAsync(c, t, """
                SELECT 1 FROM SecurityShiftProfileCredentials
                 WHERE ShiftProfileId=$id AND CredentialVersion=$version AND KdfAlgorithm=$algorithm
                   AND KdfParameters=$parameters AND Salt=$salt AND PasswordVerifier=$verifier AND IsCurrent=$current;
                """, token, ("$id", item.Credential.ShiftProfileId), ("$version", item.Credential.CredentialVersion),
                ("$algorithm", item.Credential.KdfAlgorithm), ("$parameters", item.Credential.KdfParameters),
                ("$salt", item.Credential.Salt), ("$verifier", item.Credential.PasswordVerifier),
                ("$current", item.Credential.IsCurrent ? 1 : 0)).ConfigureAwait(false))
                issues.Add("shift-profile-record-conflict");
        }
        long eventCount = await CountAsync(c, t, "SELECT COUNT(*) FROM Events WHERE StationId=$station;", token,
            ("$station", p.StationId)).ConfigureAwait(false);
        if (eventCount != p.Events.Count)
            issues.Add($"event-mapping-conflict:{eventCount}/{p.Events.Count}");
        if (await CountAsync(c, t, "SELECT COUNT(*) FROM Events WHERE StationId<>$station;", token,
                ("$station", p.StationId)).ConfigureAwait(false) != 0)
            issues.Add("cross-station-event-contamination");
        foreach (TargetEventProvisioningRecord item in p.Events)
            if (!await ExistsAsync(c, t, """
                SELECT 1 FROM Events
                 WHERE EventId=$id AND StationId=$station AND UnitId=$unit AND EventType=$type AND EventDate=$date
                   AND EventTime=$time AND EventDateTime=$datetime
                   AND ((Remark IS NULL AND $remark IS NULL) OR Remark=$remark)
                   AND CreatedAt=$created AND CreatedByShiftProfileId=$actor AND IsDeleted=0 AND RowVersion=$version;
                """, token, ("$id", item.EventId), ("$station", item.StationId), ("$unit", item.UnitId),
                ("$type", item.EventType), ("$date", item.EventDate), ("$time", item.EventTime),
                ("$datetime", item.EventDateTime), ("$remark", item.Remark is null ? DBNull.Value : item.Remark),
                ("$created", Format(item.CreatedAtUtc)), ("$actor", item.CreatedByShiftProfileId),
                ("$version", item.RowVersion)).ConfigureAwait(false))
                issues.Add("event-record-conflict");
        if (await CountAsync(c, t, "SELECT COUNT(*) FROM ReportSnapshots WHERE StationId=$station;", token, ("$station", p.StationId))
            .ConfigureAwait(false) != p.FinalizedSnapshots.Count)
            issues.Add("snapshot-mapping-conflict");
        if (await CountAsync(c, t, "SELECT COUNT(*) FROM ReportSnapshots WHERE StationId<>$station;", token,
                ("$station", p.StationId)).ConfigureAwait(false) != 0)
            issues.Add("cross-station-snapshot-contamination");
        foreach (TargetFinalizedSnapshotProvisioningRecord item in p.FinalizedSnapshots)
            if (!await ExistsAsync(c, t, """
                SELECT 1 FROM ReportSnapshots
                 WHERE SnapshotId=$id AND ReportId=$report AND StationId=$station AND PeriodStartMinute=$start
                   AND PeriodEndMinute=$end AND PeriodKind=$kind AND SnapshotSequence=$sequence
                   AND ((SupersedesSnapshotId IS NULL AND $supersedes IS NULL) OR SupersedesSnapshotId=$supersedes)
                   AND PayloadSchemaVersion=$payload AND CanonicalJson=$json AND ChecksumAlgorithm=$algorithm
                   AND IntegrityFormatVersion=$format AND ChecksumValue=$checksum AND CanonicalPayloadLength=$length
                   AND SourceRevision=$source AND FinalizedAt=$finalized;
                """, token, ("$id", item.SnapshotId), ("$report", item.ReportId), ("$station", item.StationId),
                ("$start", item.PeriodStartMinute), ("$end", item.PeriodEndMinute), ("$kind", item.PeriodKind),
                ("$sequence", item.SnapshotSequence), ("$supersedes", item.SupersedesSnapshotId is null ? DBNull.Value : item.SupersedesSnapshotId),
                ("$payload", item.PayloadSchemaVersion), ("$json", item.CanonicalJson), ("$algorithm", item.ChecksumAlgorithm),
                ("$format", item.IntegrityFormatVersion), ("$checksum", item.ChecksumValue),
                ("$length", item.CanonicalPayloadLength), ("$source", item.SourceRevision),
                ("$finalized", Format(item.FinalizedAt))).ConfigureAwait(false))
                issues.Add("snapshot-record-conflict");
        if (await CountAsync(c, t, "SELECT COUNT(*) FROM ReportPeriodLocks WHERE StationId=$station;", token, ("$station", p.StationId))
            .ConfigureAwait(false) != p.FinalizedLocks.Count)
            issues.Add("lock-mapping-conflict");
        if (await CountAsync(c, t, "SELECT COUNT(*) FROM ReportPeriodLocks WHERE StationId<>$station;", token,
                ("$station", p.StationId)).ConfigureAwait(false) != 0)
            issues.Add("cross-station-lock-contamination");
        foreach (TargetFinalizedLockProvisioningRecord item in p.FinalizedLocks)
            if (!await ExistsAsync(c, t, """
                SELECT 1 FROM ReportPeriodLocks
                 WHERE StationId=$station AND PeriodStartMinute=$start AND PeriodEndMinute=$end AND PeriodKind=$kind
                   AND LockState='Finalized' AND EffectiveSnapshotId=$snapshot AND Revision=$revision
                   AND FinalizationId=$finalization AND FinalizedAt=$at AND ActorIdentity=$actor;
                """, token, ("$station", item.StationId), ("$start", item.PeriodStartMinute),
                ("$end", item.PeriodEndMinute), ("$kind", item.PeriodKind), ("$snapshot", item.EffectiveSnapshotId),
                ("$revision", item.Revision), ("$finalization", item.FinalizationId), ("$at", Format(item.FinalizedAt)),
                ("$actor", item.ActorIdentity)).ConfigureAwait(false))
                issues.Add("lock-record-conflict");
        if (!await ExistsAsync(c, t, """
            SELECT 1 FROM SecurityManagementCredentials
             WHERE SingletonId=1 AND CredentialVersion=$version AND IsCurrent=1 AND IsActive=1
               AND KdfAlgorithm=$algorithm AND KdfParameters=$parameters AND Salt=$salt AND PasswordVerifier=$verifier;
            """, token, ("$version", p.ManagementCredential.CredentialVersion),
            ("$algorithm", p.ManagementCredential.KdfAlgorithm), ("$parameters", p.ManagementCredential.KdfParameters),
            ("$salt", p.ManagementCredential.Salt), ("$verifier", p.ManagementCredential.PasswordVerifier)).ConfigureAwait(false))
            issues.Add("management-credential-mapping-conflict");
        if (!await ExistsAsync(c, t, "SELECT 1 FROM SecurityDeviceIdentity WHERE SingletonId=1 AND DeviceId=$id AND Revision=$revision;",
                token, ("$id", p.DeviceIdentity.DeviceId), ("$revision", p.DeviceIdentity.Revision)).ConfigureAwait(false))
            issues.Add("device-mapping-conflict");
        if (!await ExistsAsync(c, t, "SELECT 1 FROM SecurityDeploymentSettings WHERE SingletonId=1 AND EsdAdjustmentCanonical=$value;",
                token, ("$value", p.EsdAdjustmentCanonical)).ConfigureAwait(false))
            issues.Add("esd-mapping-conflict");
        return issues.AsReadOnly();
    }

    private static async Task<bool> HasStationDataBeforeProvisioningAsync(SqliteConnection c, SqliteTransaction t,
        string stationId, TargetStationProvisioningPackage p, CancellationToken token) =>
        await CountAsync(c, t, "SELECT COUNT(*) FROM Stations WHERE StationId=$station;", token, ("$station", stationId))
            .ConfigureAwait(false) > 0 &&
        await CountAsync(c, t, "SELECT COUNT(*) FROM Units WHERE StationId=$station;", token, ("$station", stationId))
            .ConfigureAwait(false) >= p.Units.Count;

    private static async Task<bool> ExistsAsync(SqliteConnection c, SqliteTransaction t, string sql,
        CancellationToken token, params (string Name, object Value)[] parameters) =>
        await ScalarAsync(c, t, sql, token, parameters).ConfigureAwait(false) is not null;

    private static async Task<long> CountAsync(SqliteConnection c, SqliteTransaction t, string sql,
        CancellationToken token, params (string Name, object Value)[] parameters) =>
        Convert.ToInt64(await ScalarAsync(c, t, sql, token, parameters).ConfigureAwait(false), CultureInfo.InvariantCulture);

    private static async Task<object?> ScalarAsync(SqliteConnection c, SqliteTransaction t, string sql,
        CancellationToken token, params (string Name, object Value)[] parameters)
    {
        await using SqliteCommand command = c.CreateCommand();
        command.Transaction = t; command.CommandText = sql;
        foreach ((string name, object value) in parameters) command.Parameters.AddWithValue(name, value);
        return await command.ExecuteScalarAsync(token).ConfigureAwait(false);
    }

    private static async Task ExecuteAsync(SqliteConnection c, SqliteTransaction t, string sql,
        CancellationToken token, params (string Name, object Value)[] parameters)
    {
        await using SqliteCommand command = c.CreateCommand();
        command.Transaction = t; command.CommandText = sql;
        foreach ((string name, object value) in parameters) command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    }

    private static string Format(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);
    private static TargetProvisioningResult Rejected(TargetProvisioningFailure failure, IReadOnlyList<string> issues) =>
        new(TargetProvisioningOutcome.Rejected, failure, null, issues);
}
