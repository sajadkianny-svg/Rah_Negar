using Rah_Negar.Foundation.Application.Activation;
using Rah_Negar.Foundation.Application.Database.Readiness;
using Rah_Negar.Foundation.Time;
using Rah_Negar.Infrastructure.Database.Checksums;
using Rah_Negar.Infrastructure.Database.Migrations;
using Rah_Negar.Infrastructure.Database.Migrations.Drafts;

namespace Rah_Negar.Infrastructure.Database.Readiness;

/// <summary>
/// Explicit migration execution boundary for a previously approved context.
/// The caller supplies both database and verified-backup paths. This class has
/// no path discovery, startup registration, authority activation, or target
/// routing behavior.
/// </summary>
public sealed class ProductionMigrationExecutor : IProductionMigrationExecutor
{
    private readonly IClock _clock;
    private readonly IRestoreValidationService _restoreValidation;
    private readonly IReadOnlyDatabasePreflightAnalyzer _preflight;
    private readonly IDatabaseStructuralFingerprintService _fingerprints;
    private readonly IChecksumService _checksums;
    private readonly IDiskSpaceReadinessService _diskSpace;
    private readonly ISqliteBusyRetryExecutor _busyRetry;
    private readonly DiskSpaceSafetyPolicy _diskPolicy;
    private readonly SqliteLockBusyPolicy _lockPolicy;
    private readonly IReadOnlyList<IDatabaseMigration> _migrations;
    private readonly MigrationHistoryClassifier _classifier;

    public ProductionMigrationExecutor(
        IClock clock,
        IRestoreValidationService restoreValidation,
        IReadOnlyDatabasePreflightAnalyzer preflight,
        IDatabaseStructuralFingerprintService fingerprints,
        IChecksumService checksums,
        IDiskSpaceReadinessService diskSpace,
        ISqliteBusyRetryExecutor busyRetry,
        DiskSpaceSafetyPolicy diskPolicy,
        SqliteLockBusyPolicy lockPolicy)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _restoreValidation = restoreValidation ?? throw new ArgumentNullException(nameof(restoreValidation));
        _preflight = preflight ?? throw new ArgumentNullException(nameof(preflight));
        _fingerprints = fingerprints ?? throw new ArgumentNullException(nameof(fingerprints));
        _checksums = checksums ?? throw new ArgumentNullException(nameof(checksums));
        _diskSpace = diskSpace ?? throw new ArgumentNullException(nameof(diskSpace));
        _busyRetry = busyRetry ?? throw new ArgumentNullException(nameof(busyRetry));
        _diskPolicy = diskPolicy ?? throw new ArgumentNullException(nameof(diskPolicy));
        _diskPolicy.Validate();
        _lockPolicy = lockPolicy ?? throw new ArgumentNullException(nameof(lockPolicy));
        _migrations = UnifiedTargetMigrationChain.Create(_checksums);
        _classifier = new MigrationHistoryClassifier(
            _migrations.Select(x => new SupportedMigrationDefinition(
                x.Metadata.MigrationId, x.Metadata.FromVersion, x.Metadata.ToVersion, x.Metadata.Checksum)),
            UnifiedTargetMigrationChain.FinalVersion);
    }

    public async Task<ProductionMigrationExecutionResult> ExecuteAsync(
        ApprovedProductionMigrationContext approvedContext,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = _clock.UtcNow.ToUniversalTime();
        string correlationId = CorrelationOf(approvedContext);
        if (!ApprovedProductionMigrationContextValidator.IsValid(approvedContext, now))
            return Rejected(correlationId, "ApprovedMigrationContextRejected");

        string databasePath = Path.GetFullPath(approvedContext.ExplicitDatabasePath);
        string backupPath = Path.GetFullPath(approvedContext.ExplicitVerifiedBackupPath!);
        if (StringComparer.OrdinalIgnoreCase.Equals(databasePath, backupPath))
            return Rejected(correlationId, "DatabaseAndBackupPathMustDiffer");

        bool migrationCommitted = false;
        try
        {
            RestoreValidationResult backup = await _restoreValidation.ValidateAsync(
                backupPath, approvedContext.VerifiedBackupSha256!, cancellationToken)
                .ConfigureAwait(false);
            if (!backup.IsValid || backup.BackupIdentity is null ||
                !StringComparer.OrdinalIgnoreCase.Equals(backup.ActualSha256,
                    approvedContext.VerifiedBackupSha256) ||
                !StringComparer.Ordinal.Equals(backup.BackupIdentity.IdentityFingerprint,
                    approvedContext.EvidencePackage.BackupReceipt.BackupIdentityFingerprint) ||
                backup.MigrationState != approvedContext.EvidencePackage.Migration.Classification)
                return Rejected(correlationId, "VerifiedBackupPrerequisiteRejected");

            DatabasePreflightResult beforePreflight = await _preflight.AnalyzeAsync(
                databasePath, IntegrityCheckStrategy.FullIntegrityCheck, cancellationToken)
                .ConfigureAwait(false);
            if (!beforePreflight.Succeeded || beforePreflight.TargetInspection.Target is null ||
                !beforePreflight.HeaderValid || !beforePreflight.IntegrityPassed ||
                beforePreflight.ForeignKeyViolations.Count > 0 ||
                !beforePreflight.ReadOnlyConnectionEnforced ||
                beforePreflight.SourceFileMarkedReadOnly)
                return Rejected(correlationId, "CurrentDatabasePreflightRejected");

            DatabaseTargetDescriptor target = beforePreflight.TargetInspection.Target;
            if (!StringComparer.OrdinalIgnoreCase.Equals(target.ExplicitPath, databasePath) ||
                !StringComparer.Ordinal.Equals(target.IdentityFingerprint,
                    approvedContext.EvidencePackage.DatabaseIdentityFingerprint))
                return Rejected(correlationId, "CurrentDatabaseIdentityDoesNotMatchApproval");

            MigrationHistoryClassificationResult beforeClassification = _classifier.Classify(beforePreflight);
            if (!beforeClassification.IsMigrationChainSupported ||
                beforeClassification.Classification != approvedContext.EvidencePackage.Migration.Classification)
                return Rejected(correlationId, "CurrentMigrationClassificationDoesNotMatchApproval");

            SqliteLockReadinessResult lockReadiness = SqliteLockReadinessEvaluator.Evaluate(_lockPolicy);
            if (!lockReadiness.IsReady)
                return Rejected(correlationId, "SqliteLockPolicyNotReady");

            DiskSpaceReadinessResult disk = await _diskSpace.EvaluateAsync(
                target.FileSizeBytes, databasePath, _diskPolicy, cancellationToken)
                .ConfigureAwait(false);
            if (disk.Status != DiskSpaceReadinessStatus.Ready)
                return Rejected(correlationId, "DiskCapacityNotReady");

            DatabaseStructuralFingerprint before = await _fingerprints.CaptureAsync(
                databasePath, cancellationToken).ConfigureAwait(false);
            string originalBackupHash = await ComputeHashAsync(backupPath, cancellationToken)
                .ConfigureAwait(false);

            var factory = new SqliteConnectionFactory(new SqliteDatabaseOptions
            {
                DataSource = databasePath,
                Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWrite,
                Cache = Microsoft.Data.Sqlite.SqliteCacheMode.Private,
                Pooling = false,
                DefaultTimeoutSeconds = Math.Max(1, (int)Math.Ceiling(_lockPolicy.BusyTimeout.TotalSeconds))
            });
            var runner = new MigrationRunner(new SqliteTransactionManager(factory),
                new MigrationChecksumValidator(_checksums));

            MigrationRunResult run = await _busyRetry.ExecuteAsync(
                (_, token) => runner.RunPendingAsync(_migrations, token), cancellationToken)
                .ConfigureAwait(false);
            migrationCommitted = true;
            bool idempotentRerun = run.AppliedMigrationIds.Count == 0;

            DatabasePreflightResult afterPreflight = await _preflight.AnalyzeAsync(
                databasePath, IntegrityCheckStrategy.FullIntegrityCheck, cancellationToken)
                .ConfigureAwait(false);
            if (!afterPreflight.Succeeded || afterPreflight.TargetInspection.Target is null ||
                !afterPreflight.HeaderValid || !afterPreflight.IntegrityPassed ||
                afterPreflight.ForeignKeyViolations.Count > 0 ||
                !afterPreflight.ReadOnlyConnectionEnforced)
                return Failed(correlationId, "PostValidationFailed", null);

            MigrationHistoryClassificationResult afterClassification = _classifier.Classify(afterPreflight);
            bool ledgerProgressValid = afterClassification.Classification ==
                MigrationHistoryClassification.CleanUnifiedTarget &&
                afterPreflight.MigrationLedger.CurrentVersion == UnifiedTargetMigrationChain.FinalVersion &&
                run.FinalVersion == UnifiedTargetMigrationChain.FinalVersion;
            DatabaseStructuralFingerprint after = await _fingerprints.CaptureAsync(
                databasePath, cancellationToken).ConfigureAwait(false);
            string backupAfterHash = await ComputeHashAsync(backupPath, cancellationToken)
                .ConfigureAwait(false);
            bool backupUnchanged = StringComparer.Ordinal.Equals(originalBackupHash, backupAfterHash);
            PreservationVerificationResult preservation = DatabasePreservationVerifier.Compare(
                before, after, ledgerProgressValid);
            bool postValidationPassed = backupUnchanged && preservation.Passed;
            string receiptId = CreateReceiptId(correlationId,
                approvedContext.EvidencePackage.DatabaseIdentityFingerprint,
                approvedContext.VerifiedBackupSha256!, run.FinalVersion);
            var receipt = new ProductionMigrationValidationReceipt(
                receiptId, correlationId,
                approvedContext.EvidencePackage.DatabaseIdentityFingerprint,
                approvedContext.EvidencePackage.BackupReceipt.BackupIdentityFingerprint,
                beforeClassification.Classification,
                run.InitialVersion,
                run.FinalVersion,
                run.AppliedMigrationIds,
                idempotentRerun,
                backupUnchanged,
                beforePreflight.IntegrityPassed && beforePreflight.ForeignKeyViolations.Count == 0,
                postValidationPassed,
                preservation,
                legacyRemainsAuthoritative: true,
                targetRoutingDisabled: true,
                postValidationPassed ? OperationalRollbackState.ValidationPassed :
                    OperationalRollbackState.ValidationFailed,
                _clock.UtcNow.ToUniversalTime());

            return postValidationPassed
                ? new ProductionMigrationExecutionResult(
                    ProductionMigrationExecutionStatus.Succeeded, correlationId, receiptId,
                    idempotentRerun ? "MigrationAlreadyAtTargetAndValidated" :
                        "MigrationAppliedAndValidated", receipt)
                : new ProductionMigrationExecutionResult(
                    ProductionMigrationExecutionStatus.Failed, correlationId, receiptId,
                    "PostValidationFailedRollbackRequired", receipt);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested && !migrationCommitted)
        {
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested && migrationCommitted)
        {
            return Failed(correlationId, "MigrationCommittedCancellationRequiresValidation", null);
        }
        catch
        {
            // MigrationRunner owns the transaction and rolls back any
            // uncommitted schema/ledger changes. Once it has returned, this
            // boundary never performs an automatic restore or authority action.
            return Failed(correlationId, migrationCommitted
                ? "MigrationCommittedValidationFailedRollbackRequired"
                : "MigrationFailedTransactionRolledBack", null);
        }
    }

    private static ProductionMigrationExecutionResult Rejected(string correlationId, string category) =>
        new(ProductionMigrationExecutionStatus.Rejected, correlationId, null, category);

    private static ProductionMigrationExecutionResult Failed(string correlationId, string category,
        ProductionMigrationValidationReceipt? receipt) =>
        new(ProductionMigrationExecutionStatus.Failed, correlationId, receipt?.ReceiptId, category, receipt);

    private static string CorrelationOf(ApprovedProductionMigrationContext? context) =>
        context?.EvidencePackage?.CorrelationId is { Length: > 0 } correlation ? correlation : "none";

    private static string CreateReceiptId(string correlationId, string databaseIdentity,
        string backupSha256, int finalVersion) =>
        "migration-" + ExplicitDatabaseTargetInspector.HashCanonical(
            $"{correlationId}|{databaseIdentity}|{backupSha256}|{finalVersion}")[..24].ToLowerInvariant();

    private static Task<string> ComputeHashAsync(string path, CancellationToken token) =>
        ReadinessFileHash.ComputeSha256Async(path, token);
}
