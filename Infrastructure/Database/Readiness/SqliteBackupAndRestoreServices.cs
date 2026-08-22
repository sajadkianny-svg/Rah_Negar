using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Rah_Negar.Foundation.Application.Database.Readiness;
using Rah_Negar.Foundation.Time;

namespace Rah_Negar.Infrastructure.Database.Readiness;

public sealed class ExplicitSqliteBackupService : IExplicitSqliteBackupService
{
    private readonly IReadOnlyDatabasePreflightAnalyzer _preflight;
    private readonly IDatabaseStructuralFingerprintService _fingerprints;
    private readonly MigrationHistoryClassifier _classifier;
    private readonly IClock _clock;

    public ExplicitSqliteBackupService(IReadOnlyDatabasePreflightAnalyzer preflight,
        IDatabaseStructuralFingerprintService fingerprints, MigrationHistoryClassifier classifier, IClock clock)
    {
        _preflight = preflight ?? throw new ArgumentNullException(nameof(preflight));
        _fingerprints = fingerprints ?? throw new ArgumentNullException(nameof(fingerprints));
        _classifier = classifier ?? throw new ArgumentNullException(nameof(classifier));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<DatabaseBackupVerificationResult> CreateVerifiedBackupAsync(
        string explicitSourcePath, string explicitDestinationPath, BackupOverwritePolicy overwritePolicy,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(explicitSourcePath))
            return Fail(explicitDestinationPath, DatabaseBackupFailure.InvalidSource, "ExplicitSourceRequired");
        if (string.IsNullOrWhiteSpace(explicitDestinationPath))
            return Fail(string.Empty, DatabaseBackupFailure.InvalidDestination, "ExplicitDestinationRequired");
        string source;
        string destination;
        try
        {
            source = Path.GetFullPath(explicitSourcePath);
            destination = Path.GetFullPath(explicitDestinationPath);
        }
        catch
        {
            return Fail(explicitDestinationPath, DatabaseBackupFailure.InvalidDestination, "InvalidExplicitPath");
        }
        if (StringComparer.OrdinalIgnoreCase.Equals(source, destination))
            return Fail(destination, DatabaseBackupFailure.SameSourceAndDestination, "SourceEqualsDestination");
        string? directory = Path.GetDirectoryName(destination);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return Fail(destination, DatabaseBackupFailure.InvalidDestination, "DestinationDirectoryMissing");
        if (File.Exists(destination) && overwritePolicy == BackupOverwritePolicy.Deny)
            return Fail(destination, DatabaseBackupFailure.DestinationExists, "DestinationExists");

        DatabasePreflightResult sourcePreflight = await _preflight.AnalyzeAsync(source,
            IntegrityCheckStrategy.FullIntegrityCheck, cancellationToken).ConfigureAwait(false);
        if (!sourcePreflight.Succeeded || sourcePreflight.TargetInspection.Target is null)
            return Fail(destination, DatabaseBackupFailure.InvalidSource, "SourcePreflightFailed");
        DatabaseStructuralFingerprint sourceBefore = await _fingerprints.CaptureAsync(source, cancellationToken)
            .ConfigureAwait(false);
        try
        {
            if (File.Exists(destination) && overwritePolicy == BackupOverwritePolicy.Allow)
            {
                File.Delete(destination);
                DeleteSidecar(destination + "-wal");
                DeleteSidecar(destination + "-shm");
            }
            await using SqliteConnection sourceConnection = await ReadOnlyDatabasePreflightAnalyzer.OpenReadOnlyAsync(
                source, cancellationToken).ConfigureAwait(false);
            var destinationBuilder = new SqliteConnectionStringBuilder
            {
                DataSource = destination,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Private,
                Pooling = false,
                DefaultTimeout = 10
            };
            await using var destinationConnection = new SqliteConnection(destinationBuilder.ToString());
            await destinationConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            sourceConnection.BackupDatabase(destinationConnection);
            await destinationConnection.CloseAsync().ConfigureAwait(false);

            DatabasePreflightResult backupPreflight = await _preflight.AnalyzeAsync(destination,
                IntegrityCheckStrategy.FullIntegrityCheck, cancellationToken).ConfigureAwait(false);
            string checksum = await ReadinessFileHash.ComputeSha256Async(destination, cancellationToken)
                .ConfigureAwait(false);
            DatabaseStructuralFingerprint sourceAfter = await _fingerprints.CaptureAsync(source, cancellationToken)
                .ConfigureAwait(false);
            if (!StringComparer.Ordinal.Equals(sourceBefore.Sha256, sourceAfter.Sha256))
                return Result(false, destination, sourcePreflight.TargetInspection.Target,
                    backupPreflight.TargetInspection.Target, checksum, DatabaseBackupFailure.SourceChanged,
                    backupPreflight, "SourceChangedDuringBackup");
            if (!backupPreflight.Succeeded || !backupPreflight.IntegrityPassed ||
                backupPreflight.ForeignKeyViolations.Count > 0)
                return Result(false, destination, sourcePreflight.TargetInspection.Target,
                    backupPreflight.TargetInspection.Target, checksum, DatabaseBackupFailure.IntegrityFailed,
                    backupPreflight, "BackupIntegrityFailed");
            return Result(true, destination, sourcePreflight.TargetInspection.Target,
                backupPreflight.TargetInspection.Target, checksum, DatabaseBackupFailure.None,
                backupPreflight, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            return Fail(destination, DatabaseBackupFailure.BackupFailed, "SqliteBackupFailed",
                sourcePreflight.TargetInspection.Target);
        }
    }

    private DatabaseBackupVerificationResult Result(bool verified, string path,
        DatabaseTargetDescriptor source, DatabaseTargetDescriptor? backup, string checksum,
        DatabaseBackupFailure failure, DatabasePreflightResult preflight, string? error)
    {
        MigrationHistoryClassification classification = _classifier.Classify(preflight).Classification;
        return new(verified, path, source, backup, checksum, backup?.FileSizeBytes ?? 0,
            _clock.UtcNow.ToUniversalTime(), preflight.MigrationLedger.CurrentVersion ?? 0,
            classification, preflight.IntegrityPassed, failure,
            error is null ? Array.Empty<string>() : [error]);
    }

    private DatabaseBackupVerificationResult Fail(string path, DatabaseBackupFailure failure,
        string error, DatabaseTargetDescriptor? source = null) =>
        new(false, path ?? string.Empty, source, null, null, 0, _clock.UtcNow.ToUniversalTime(), 0,
            MigrationHistoryClassification.UnsafeToMigrate, false, failure, [error]);

    private static void DeleteSidecar(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }
}

public sealed class RestoreValidationService : IRestoreValidationService
{
    private readonly IReadOnlyDatabasePreflightAnalyzer _preflight;
    private readonly MigrationHistoryClassifier _classifier;

    public RestoreValidationService(IReadOnlyDatabasePreflightAnalyzer preflight,
        MigrationHistoryClassifier classifier)
    {
        _preflight = preflight ?? throw new ArgumentNullException(nameof(preflight));
        _classifier = classifier ?? throw new ArgumentNullException(nameof(classifier));
    }

    public async Task<RestoreValidationResult> ValidateAsync(string explicitBackupPath,
        string expectedSha256, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(explicitBackupPath) || string.IsNullOrWhiteSpace(expectedSha256))
            return Fail(RestoreValidationFailure.PathRequired, "ExplicitBackupAndChecksumRequired");
        string path;
        try { path = Path.GetFullPath(explicitBackupPath); }
        catch { return Fail(RestoreValidationFailure.PathRequired, "InvalidExplicitBackupPath"); }
        if (!File.Exists(path)) return Fail(RestoreValidationFailure.Missing, "BackupFileMissing");
        string actual;
        try { actual = await ReadinessFileHash.ComputeSha256Async(path, cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return Fail(RestoreValidationFailure.InvalidSqlite, "BackupReadFailed"); }
        if (!StringComparer.OrdinalIgnoreCase.Equals(actual, expectedSha256))
            return new(false, null, actual, 0, MigrationHistoryClassification.UnsafeToMigrate,
                false, RestoreValidationFailure.ChecksumMismatch, ["BackupChecksumMismatch"]);
        DatabasePreflightResult preflight = await _preflight.AnalyzeAsync(path,
            IntegrityCheckStrategy.FullIntegrityCheck, cancellationToken).ConfigureAwait(false);
        if (!preflight.Succeeded || preflight.TargetInspection.Target is null)
            return new(false, null, actual, 0, MigrationHistoryClassification.UnsafeToMigrate,
                false, RestoreValidationFailure.InvalidSqlite, ["BackupPreflightFailed"]);
        MigrationHistoryClassificationResult classification = _classifier.Classify(preflight);
        if (!preflight.IntegrityPassed || preflight.ForeignKeyViolations.Count > 0)
            return new(false, preflight.TargetInspection.Target, actual,
                preflight.MigrationLedger.CurrentVersion ?? 0, classification.Classification,
                false, RestoreValidationFailure.IntegrityFailed, ["BackupIntegrityFailed"]);
        if (classification.Classification is MigrationHistoryClassification.ChecksumMismatch or
            MigrationHistoryClassification.CorruptMigrationHistory or
            MigrationHistoryClassification.LedgerSchemaMismatch or
            MigrationHistoryClassification.UnknownMigrationHistory or
            MigrationHistoryClassification.UnsupportedNewerVersion or
            MigrationHistoryClassification.UnsafeToMigrate)
            return new(false, preflight.TargetInspection.Target, actual,
                preflight.MigrationLedger.CurrentVersion ?? 0, classification.Classification,
                true, RestoreValidationFailure.UnsupportedMigrationState, classification.Reasons);
        return new(true, preflight.TargetInspection.Target, actual,
            preflight.MigrationLedger.CurrentVersion ?? 0, classification.Classification,
            true, RestoreValidationFailure.None, Array.Empty<string>());
    }

    private static RestoreValidationResult Fail(RestoreValidationFailure failure, string error) =>
        new(false, null, null, 0, MigrationHistoryClassification.UnsafeToMigrate,
            false, failure, [error]);
}

internal static class ReadinessFileHash
{
    public static async Task<string> ComputeSha256Async(string explicitPath, CancellationToken token)
    {
        await using var stream = new FileStream(explicitPath, FileMode.Open, FileAccess.Read,
            FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        byte[] hash = await SHA256.HashDataAsync(stream, token).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }
}
