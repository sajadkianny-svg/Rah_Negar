using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Rah_Negar.Foundation.Application.Database.Readiness;
using Rah_Negar.Foundation.Application.Security;
using Rah_Negar.Foundation.Time;

namespace Rah_Negar.Infrastructure.Database.Readiness;

/// <summary>
/// The isolated protected boundary for verified backup acceptance and staged restore.
/// It never changes authority and it only operates on caller-supplied explicit paths.
/// </summary>
public sealed class ManagedSqliteBackupRestoreBoundary : IManagedSqliteBackupRestoreBoundary
{
    private readonly ExplicitSqliteBackupService _backup;
    private readonly RestoreValidationService _restoreValidation;
    private readonly IReadOnlyDatabasePreflightAnalyzer _preflight;
    private readonly IClock _clock;

    public ManagedSqliteBackupRestoreBoundary(
        ExplicitSqliteBackupService backup,
        RestoreValidationService restoreValidation,
        IReadOnlyDatabasePreflightAnalyzer preflight,
        IClock clock)
    {
        _backup = backup ?? throw new ArgumentNullException(nameof(backup));
        _restoreValidation = restoreValidation ?? throw new ArgumentNullException(nameof(restoreValidation));
        _preflight = preflight ?? throw new ArgumentNullException(nameof(preflight));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<ManagedSqliteBackupResult> CreateVerifiedBackupAsync(
        string explicitSourcePath,
        string explicitDestinationPath,
        BackupOverwritePolicy overwritePolicy,
        ManagementAuthorizationProof managementProof,
        int currentManagementCredentialVersion,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = _clock.UtcNow.ToUniversalTime();
        string correlationId = managementProof?.CorrelationId ?? string.Empty;
        string source = string.Empty;
        string destination = string.Empty;
        string scope = string.Empty;
        try
        {
            source = Path.GetFullPath(explicitSourcePath);
            destination = Path.GetFullPath(explicitDestinationPath);
            scope = SqliteProtectedActionBinding.CreateBackupScope(source, destination, overwritePolicy);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return BackupFailure(now, correlationId, source, destination, scope,
                managementProof?.InitiatingShiftProfileId ?? string.Empty,
                SqliteBoundaryFailure.InvalidPath, "InvalidExplicitBackupPath");
        }
        if (managementProof is null)
            return BackupFailure(now, correlationId, source, destination, scope, string.Empty,
                SqliteBoundaryFailure.AuthorizationRejected, "ManagementCredentialProofRequired");

        SqliteBoundaryFailure? authorizationFailure = ValidateAuthorization(managementProof,
            ProtectedAction.BackupPolicy, scope, currentManagementCredentialVersion, now);
        if (authorizationFailure is not null)
            return BackupFailure(now, correlationId, source, destination, scope,
                managementProof.InitiatingShiftProfileId, authorizationFailure.Value,
                "ManagementCredentialProofRejected");

        if (!File.Exists(source))
            return BackupFailure(now, correlationId, source, destination, scope,
                managementProof.InitiatingShiftProfileId, SqliteBoundaryFailure.SourceMissing, "SourceDatabaseMissing");

        DatabasePreflightResult sourcePreflight = await _preflight.AnalyzeAsync(source,
            IntegrityCheckStrategy.FullIntegrityCheck, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<SqliteSidecarEvidence> sidecarsBefore = await CaptureSidecarsAsync(source, cancellationToken)
            .ConfigureAwait(false);
        if (!sourcePreflight.Succeeded || !sourcePreflight.IntegrityPassed ||
            sourcePreflight.ForeignKeyViolations.Count != 0)
            return BackupFailure(now, correlationId, source, destination, scope,
                managementProof.InitiatingShiftProfileId, SqliteBoundaryFailure.PreflightFailed,
                "SourcePreflightFailed", sidecarsBefore, journalMode: sourcePreflight.JournalMode);

        string? sourceSha256 = await TryHashAsync(source, cancellationToken).ConfigureAwait(false);
        DatabaseBackupVerificationResult verification = await _backup.CreateVerifiedBackupAsync(
            source, destination, overwritePolicy, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<SqliteSidecarEvidence> sidecarsAfter = await CaptureSidecarsAsync(source, cancellationToken)
            .ConfigureAwait(false);
        if (!SidecarsEqual(sidecarsBefore, sidecarsAfter))
            return BackupFailure(now, correlationId, source, destination, scope,
                managementProof.InitiatingShiftProfileId, SqliteBoundaryFailure.SidecarChanged,
                "SourceSidecarChangedDuringBackup", sidecarsAfter, sourceSha256: sourceSha256,
                journalMode: sourcePreflight.JournalMode, verification: verification);

        SqliteBoundaryFailure failure = verification.IsVerified
            ? SqliteBoundaryFailure.None
            : MapBackupFailure(verification.Failure);
        var receipt = new SqliteBackupReceipt(
            CreateReceiptId("backup", managementProof.CorrelationId, now), managementProof.CorrelationId,
            scope, managementProof.InitiatingShiftProfileId, source, destination, sourceSha256,
            verification.BackupSha256, verification.BackupSizeBytes, sourcePreflight.JournalMode,
            sidecarsBefore, verification.IntegrityPassed,
            verification.BackupIdentity is not null && verification.IsVerified, verification.IsVerified,
            failure, now);
        return new(verification.IsVerified, failure, receipt, verification, verification.Errors);
    }

    public async Task<ManagedSqliteRestoreResult> RestoreAsync(
        string explicitBackupPath,
        string expectedBackupSha256,
        string explicitDestinationPath,
        string explicitRollbackCopyPath,
        ManagementAuthorizationProof managementProof,
        int currentManagementCredentialVersion,
        SqliteRestoreFailureInjectionPoint failureInjection = SqliteRestoreFailureInjectionPoint.None,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = _clock.UtcNow.ToUniversalTime();
        string correlationId = managementProof?.CorrelationId ?? string.Empty;
        string backup = string.Empty;
        string destination = string.Empty;
        string rollback = string.Empty;
        string scope = string.Empty;
        try
        {
            backup = Path.GetFullPath(explicitBackupPath);
            destination = Path.GetFullPath(explicitDestinationPath);
            rollback = Path.GetFullPath(explicitRollbackCopyPath);
            scope = SqliteProtectedActionBinding.CreateRestoreScope(backup, expectedBackupSha256,
                destination, rollback);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return RestoreFailure(now, correlationId, backup, destination, rollback, scope,
                managementProof?.InitiatingShiftProfileId ?? string.Empty,
                expectedBackupSha256, SqliteBoundaryFailure.InvalidPath, "InvalidExplicitRestorePath");
        }
        if (managementProof is null)
            return RestoreFailure(now, correlationId, backup, destination, rollback, scope, string.Empty,
                expectedBackupSha256, SqliteBoundaryFailure.AuthorizationRejected,
                "ManagementCredentialProofRequired");

        SqliteBoundaryFailure? authorizationFailure = ValidateAuthorization(managementProof,
            ProtectedAction.Restore, scope, currentManagementCredentialVersion, now);
        if (authorizationFailure is not null)
            return RestoreFailure(now, correlationId, backup, destination, rollback, scope,
                managementProof.InitiatingShiftProfileId, expectedBackupSha256,
                authorizationFailure.Value, "ManagementCredentialProofRejected");

        if (!IsSha256(expectedBackupSha256))
            return RestoreFailure(now, correlationId, backup, destination, rollback, scope,
                managementProof.InitiatingShiftProfileId, expectedBackupSha256,
                SqliteBoundaryFailure.InvalidChecksum, "ExpectedSha256Invalid");
        if (!File.Exists(backup))
            return RestoreFailure(now, correlationId, backup, destination, rollback, scope,
                managementProof.InitiatingShiftProfileId, expectedBackupSha256,
                SqliteBoundaryFailure.SourceMissing, "BackupDatabaseMissing");
        if (!File.Exists(destination))
            return RestoreFailure(now, correlationId, backup, destination, rollback, scope,
                managementProof.InitiatingShiftProfileId, expectedBackupSha256,
                SqliteBoundaryFailure.DestinationMissing, "DestinationDatabaseMissing");
        if (StringComparer.OrdinalIgnoreCase.Equals(backup, destination) ||
            StringComparer.OrdinalIgnoreCase.Equals(backup, rollback) ||
            StringComparer.OrdinalIgnoreCase.Equals(destination, rollback))
            return RestoreFailure(now, correlationId, backup, destination, rollback, scope,
                managementProof.InitiatingShiftProfileId, expectedBackupSha256,
                SqliteBoundaryFailure.InvalidPath, "RestorePathsMustBeDistinct");

        string staging = destination + ".stage-" + BindingToken(scope);
        string prior = destination + ".prior-" + BindingToken(scope);
        string failed = destination + ".failed-" + BindingToken(scope);
        if (File.Exists(rollback) || File.Exists(staging) || File.Exists(prior) || File.Exists(failed) ||
            File.Exists(rollback + "-wal") || File.Exists(rollback + "-shm"))
            return RestoreFailure(now, correlationId, backup, destination, rollback, scope,
                managementProof.InitiatingShiftProfileId, expectedBackupSha256,
                SqliteBoundaryFailure.ArtifactCollision, "RestoreArtifactAlreadyExists");
        string? rollbackDirectory = Path.GetDirectoryName(rollback);
        string? destinationDirectory = Path.GetDirectoryName(destination);
        if (string.IsNullOrWhiteSpace(rollbackDirectory) || !Directory.Exists(rollbackDirectory) ||
            string.IsNullOrWhiteSpace(destinationDirectory) || !Directory.Exists(destinationDirectory))
            return RestoreFailure(now, correlationId, backup, destination, rollback, scope,
                managementProof.InitiatingShiftProfileId, expectedBackupSha256,
                SqliteBoundaryFailure.InvalidPath, "RestoreDirectoryMissing");

        DatabasePreflightResult destinationBefore = await _preflight.AnalyzeAsync(destination,
            IntegrityCheckStrategy.FullIntegrityCheck, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<SqliteSidecarEvidence> destinationSidecars = await CaptureSidecarsAsync(destination,
            cancellationToken).ConfigureAwait(false);
        string? destinationBeforeSha256 = await TryHashAsync(destination, cancellationToken).ConfigureAwait(false);
        if (!destinationBefore.Succeeded || !destinationBefore.IntegrityPassed ||
            destinationBefore.ForeignKeyViolations.Count != 0 || destinationBeforeSha256 is null)
            return RestoreFailure(now, correlationId, backup, destination, rollback, scope,
                managementProof.InitiatingShiftProfileId, expectedBackupSha256,
                SqliteBoundaryFailure.PreflightFailed, "DestinationPreflightFailed", destinationSidecars,
                destinationBeforeSha256, preRestorePassed: false);

        RestoreValidationResult backupValidation = await _restoreValidation.ValidateAsync(
            backup, expectedBackupSha256, cancellationToken).ConfigureAwait(false);
        if (!backupValidation.IsValid)
            return RestoreFailure(now, correlationId, backup, destination, rollback, scope,
                managementProof.InitiatingShiftProfileId, expectedBackupSha256,
                SqliteBoundaryFailure.BackupValidationFailed, "BackupValidationFailed", destinationSidecars,
                destinationBeforeSha256, preRestorePassed: true);

        bool priorMoved = false;
        bool walMoved = false;
        bool shmMoved = false;
        bool replacementMoved = false;
        string? rollbackSha256 = null;
        try
        {
            DatabaseBackupVerificationResult rollbackResult = await _backup.CreateVerifiedBackupAsync(
                destination, rollback, BackupOverwritePolicy.Deny, cancellationToken).ConfigureAwait(false);
            if (!rollbackResult.IsVerified || string.IsNullOrWhiteSpace(rollbackResult.BackupSha256))
                return RestoreFailure(now, correlationId, backup, destination, rollback, scope,
                    managementProof.InitiatingShiftProfileId, expectedBackupSha256,
                    SqliteBoundaryFailure.RollbackCopyFailed, "RollbackCopyVerificationFailed",
                    destinationSidecars, destinationBeforeSha256, preRestorePassed: true);
            rollbackSha256 = rollbackResult.BackupSha256;
            File.SetAttributes(rollback, File.GetAttributes(rollback) | FileAttributes.ReadOnly);
            Inject(failureInjection, SqliteRestoreFailureInjectionPoint.AfterRollbackCopy);

            await CopyAndFlushAsync(backup, staging, cancellationToken).ConfigureAwait(false);
            string stagedSha256 = await ReadinessFileHash.ComputeSha256Async(staging, cancellationToken)
                .ConfigureAwait(false);
            DatabasePreflightResult stagedPreflight = await _preflight.AnalyzeAsync(staging,
                IntegrityCheckStrategy.FullIntegrityCheck, cancellationToken).ConfigureAwait(false);
            if (!StringComparer.OrdinalIgnoreCase.Equals(stagedSha256, expectedBackupSha256) ||
                !stagedPreflight.Succeeded || !stagedPreflight.IntegrityPassed ||
                stagedPreflight.ForeignKeyViolations.Count != 0)
                return await FailAndRecoverAsync(SqliteBoundaryFailure.StagingFailed,
                    "StagedBackupValidationFailed", now, correlationId, backup, destination, rollback, scope,
                    managementProof.InitiatingShiftProfileId, expectedBackupSha256, destinationSidecars,
                    destinationBeforeSha256, rollbackSha256, staging, prior, failed, priorMoved, walMoved,
                    shmMoved, replacementMoved, cancellationToken).ConfigureAwait(false);
            Inject(failureInjection, SqliteRestoreFailureInjectionPoint.AfterStaging);

            IReadOnlyList<SqliteSidecarEvidence> sidecarsBeforeMove = await CaptureSidecarsAsync(destination,
                cancellationToken).ConfigureAwait(false);
            if (!SidecarsEqual(destinationSidecars, sidecarsBeforeMove))
                return await FailAndRecoverAsync(SqliteBoundaryFailure.SidecarChanged,
                    "DestinationSidecarChangedBeforeSwap", now, correlationId, backup, destination, rollback,
                    scope, managementProof.InitiatingShiftProfileId, expectedBackupSha256, destinationSidecars,
                    destinationBeforeSha256, rollbackSha256, staging, prior, failed, priorMoved, walMoved,
                    shmMoved, replacementMoved, cancellationToken).ConfigureAwait(false);

            File.Move(destination, prior);
            priorMoved = true;
            MoveSidecarIfPresent(destination + "-wal", prior + "-wal", ref walMoved);
            MoveSidecarIfPresent(destination + "-shm", prior + "-shm", ref shmMoved);
            Inject(failureInjection, SqliteRestoreFailureInjectionPoint.AfterPriorLiveMoved);

            File.Move(staging, destination);
            replacementMoved = true;
            Inject(failureInjection, SqliteRestoreFailureInjectionPoint.AfterSwapBeforeValidation);

            DatabasePreflightResult destinationAfter = await _preflight.AnalyzeAsync(destination,
                IntegrityCheckStrategy.FullIntegrityCheck, cancellationToken).ConfigureAwait(false);
            string destinationAfterSha256 = await ReadinessFileHash.ComputeSha256Async(destination,
                cancellationToken).ConfigureAwait(false);
            if (!destinationAfter.Succeeded || !destinationAfter.IntegrityPassed ||
                destinationAfter.ForeignKeyViolations.Count != 0 ||
                !StringComparer.OrdinalIgnoreCase.Equals(destinationAfterSha256, expectedBackupSha256))
                return await FailAndRecoverAsync(SqliteBoundaryFailure.PostRestoreValidationFailed,
                    "PostRestoreValidationFailed", now, correlationId, backup, destination, rollback, scope,
                    managementProof.InitiatingShiftProfileId, expectedBackupSha256, destinationSidecars,
                    destinationBeforeSha256, rollbackSha256, staging, prior, failed, priorMoved, walMoved,
                    shmMoved, replacementMoved, cancellationToken, destinationAfterSha256).ConfigureAwait(false);

            var receipt = CreateRestoreReceipt(now, correlationId, scope,
                managementProof.InitiatingShiftProfileId, backup, destination, rollback,
                expectedBackupSha256, destinationBeforeSha256, rollbackSha256, destinationAfterSha256,
                destinationSidecars, true, true, true, SqliteBoundaryFailure.None);
            return new(true, SqliteBoundaryFailure.None, receipt, Array.Empty<string>());
        }
        catch (InjectedRestoreFailureException ex)
        {
            return await FailAndRecoverAsync(SqliteBoundaryFailure.FailureInjected, ex.Message, now,
                correlationId, backup, destination, rollback, scope, managementProof.InitiatingShiftProfileId,
                expectedBackupSha256, destinationSidecars, destinationBeforeSha256, rollbackSha256,
                staging, prior, failed, priorMoved, walMoved, shmMoved, replacementMoved,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return await FailAndRecoverAsync(SqliteBoundaryFailure.SwapFailed, "RestoreSwapFailed", now,
                correlationId, backup, destination, rollback, scope, managementProof.InitiatingShiftProfileId,
                expectedBackupSha256, destinationSidecars, destinationBeforeSha256, rollbackSha256,
                staging, prior, failed, priorMoved, walMoved, shmMoved, replacementMoved,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<ManagedSqliteRestoreResult> FailAndRecoverAsync(
        SqliteBoundaryFailure failure, string error, DateTimeOffset now, string correlationId,
        string backup, string destination, string rollback, string scope, string shiftProfileId,
        string expectedBackupSha256, IReadOnlyList<SqliteSidecarEvidence> sidecars,
        string? destinationBeforeSha256, string? rollbackSha256, string staging, string prior, string failed,
        bool priorMoved, bool walMoved, bool shmMoved, bool replacementMoved,
        CancellationToken cancellationToken, string? destinationAfterSha256 = null)
    {
        bool recovered = true;
        try
        {
            if (replacementMoved && File.Exists(destination))
                File.Move(destination, failed);
            if (priorMoved && !File.Exists(destination))
            {
                if (walMoved && File.Exists(prior + "-wal")) File.Move(prior + "-wal", destination + "-wal");
                if (shmMoved && File.Exists(prior + "-shm")) File.Move(prior + "-shm", destination + "-shm");
                File.Move(prior, destination);
            }
            if (File.Exists(staging)) File.Delete(staging);
        }
        catch
        {
            recovered = false;
        }
        SqliteBoundaryFailure finalFailure = recovered ? failure : SqliteBoundaryFailure.RecoveryFailed;
        string finalError = recovered ? error : "RestoreRecoveryFailed";
        var receipt = CreateRestoreReceipt(now, correlationId, scope, shiftProfileId, backup, destination,
            rollback, expectedBackupSha256, destinationBeforeSha256, rollbackSha256, destinationAfterSha256,
            sidecars, true, false, false, finalFailure);
        await Task.CompletedTask.ConfigureAwait(false);
        return new(false, finalFailure, receipt, [finalError]);
    }

    private static SqliteBoundaryFailure? ValidateAuthorization(ManagementAuthorizationProof? proof,
        ProtectedAction expectedAction, string expectedScope, int currentVersion, DateTimeOffset now)
    {
        if (proof is null || currentVersion <= 0 || string.IsNullOrWhiteSpace(proof.CorrelationId) ||
            string.IsNullOrWhiteSpace(proof.InitiatingShiftProfileId))
            return SqliteBoundaryFailure.AuthorizationRejected;
        ManagementProofValidationResult validation = ManagementAuthorizationProofValidator.Validate(
            proof, proof.InitiatingShiftProfileId, expectedAction, expectedScope, proof.CorrelationId,
            currentVersion, now);
        return validation.IsValid ? null : SqliteBoundaryFailure.AuthorizationRejected;
    }

    private static void Inject(SqliteRestoreFailureInjectionPoint actual,
        SqliteRestoreFailureInjectionPoint expected)
    {
        if (actual == expected) throw new InjectedRestoreFailureException($"InjectedFailure:{expected}");
    }

    private static void MoveSidecarIfPresent(string source, string destination, ref bool moved)
    {
        if (!File.Exists(source)) return;
        File.Move(source, destination);
        moved = true;
    }

    private static async Task CopyAndFlushAsync(string source, string destination, CancellationToken token)
    {
        await using FileStream input = new(source, FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using FileStream output = new(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await input.CopyToAsync(output, token).ConfigureAwait(false);
        await output.FlushAsync(token).ConfigureAwait(false);
        output.Flush(flushToDisk: true);
    }

    private static async Task<IReadOnlyList<SqliteSidecarEvidence>> CaptureSidecarsAsync(
        string databasePath, CancellationToken token)
    {
        var evidence = new List<SqliteSidecarEvidence>(2);
        foreach (string suffix in new[] { "-wal", "-shm" })
        {
            string path = databasePath + suffix;
            if (!File.Exists(path))
            {
                evidence.Add(new(suffix, false, 0, null));
                continue;
            }
            FileInfo info = new(path);
            evidence.Add(new(suffix, true, info.Length,
                await ReadinessFileHash.ComputeSha256Async(path, token).ConfigureAwait(false)));
        }
        return evidence;
    }

    private static bool SidecarsEqual(IReadOnlyList<SqliteSidecarEvidence> left,
        IReadOnlyList<SqliteSidecarEvidence> right) =>
        left.Count == right.Count && left.Zip(right).All(pair =>
            StringComparer.Ordinal.Equals(pair.First.Suffix, pair.Second.Suffix) &&
            pair.First.Present == pair.Second.Present && pair.First.SizeBytes == pair.Second.SizeBytes &&
            StringComparer.OrdinalIgnoreCase.Equals(pair.First.Sha256, pair.Second.Sha256));

    private static async Task<string?> TryHashAsync(string path, CancellationToken token)
    {
        try { return await ReadinessFileHash.ComputeSha256Async(path, token).ConfigureAwait(false); }
        catch { return null; }
    }

    private static bool IsSha256(string value)
    {
        if (value is null || value.Trim().Length != 64) return false;
        try { Convert.FromHexString(value); return true; }
        catch (FormatException) { return false; }
    }

    private static string BindingToken(string scope) => scope[^16..].ToLowerInvariant();

    private string CreateReceiptId(string operation, string correlationId, DateTimeOffset now) =>
        $"{operation}:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{operation}|{correlationId}|{now.UtcTicks.ToString(CultureInfo.InvariantCulture)}")))[..24]}";

    private ManagedSqliteBackupResult BackupFailure(DateTimeOffset now, string correlationId,
        string source, string destination, string scope, string shiftProfileId,
        SqliteBoundaryFailure failure, string error,
        IReadOnlyList<SqliteSidecarEvidence>? sidecars = null, string? sourceSha256 = null,
        string? journalMode = null, DatabaseBackupVerificationResult? verification = null)
    {
        var actualVerification = verification ?? new(false, destination, null, null, null, 0, now, 0,
            MigrationHistoryClassification.UnsafeToMigrate, false, DatabaseBackupFailure.BackupFailed, [error]);
        var receipt = new SqliteBackupReceipt(CreateReceiptId("backup", correlationId, now), correlationId,
            scope, shiftProfileId, source, destination, sourceSha256, actualVerification.BackupSha256,
            actualVerification.BackupSizeBytes, journalMode ?? "unknown", sidecars ?? EmptySidecars(),
            actualVerification.IntegrityPassed, actualVerification.BackupIdentity is not null && actualVerification.IsVerified,
            false, failure, now);
        return new(false, failure, receipt, actualVerification, [error]);
    }

    private static SqliteBoundaryFailure MapBackupFailure(DatabaseBackupFailure failure) => failure switch
    {
        DatabaseBackupFailure.InvalidSource => SqliteBoundaryFailure.SourceMissing,
        DatabaseBackupFailure.InvalidDestination => SqliteBoundaryFailure.InvalidPath,
        DatabaseBackupFailure.SameSourceAndDestination => SqliteBoundaryFailure.InvalidPath,
        DatabaseBackupFailure.DestinationExists => SqliteBoundaryFailure.ArtifactCollision,
        DatabaseBackupFailure.SourceChanged => SqliteBoundaryFailure.SidecarChanged,
        DatabaseBackupFailure.IntegrityFailed => SqliteBoundaryFailure.BackupValidationFailed,
        _ => SqliteBoundaryFailure.BackupValidationFailed
    };

    private SqliteRestoreReceipt CreateRestoreReceipt(DateTimeOffset now, string correlationId,
        string scope, string shiftProfileId, string backup, string destination, string rollback,
        string? expected, string? destinationBefore, string? rollbackSha256, string? destinationAfter,
        IReadOnlyList<SqliteSidecarEvidence> sidecars, bool preRestore, bool postRestore,
        bool succeeded, SqliteBoundaryFailure failure) =>
        new(CreateReceiptId("restore", correlationId, now), correlationId, scope, shiftProfileId, backup,
            destination, rollback, expected, destinationBefore, rollbackSha256, destinationAfter, sidecars,
            preRestore, postRestore, succeeded, failure, now);

    private ManagedSqliteRestoreResult RestoreFailure(DateTimeOffset now, string correlationId,
        string backup, string destination, string rollback, string scope, string shiftProfileId,
        string? expected, SqliteBoundaryFailure failure, string error,
        IReadOnlyList<SqliteSidecarEvidence>? sidecars = null, string? destinationBefore = null,
        bool preRestorePassed = false) =>
        new(false, failure, CreateRestoreReceipt(now, correlationId, scope, shiftProfileId, backup,
            destination, rollback, expected, destinationBefore, null, null, sidecars ?? EmptySidecars(),
            preRestorePassed, false, false, failure), [error]);

    private static IReadOnlyList<SqliteSidecarEvidence> EmptySidecars() =>
        [new("-wal", false, 0, null), new("-shm", false, 0, null)];

    private sealed class InjectedRestoreFailureException(string message) : Exception(message);
}
