using Microsoft.Data.Sqlite;
using Rah_Negar.Foundation.Application.Database.Readiness;
using Rah_Negar.Foundation.Application.Security;
using Rah_Negar.Foundation.Time;
using Rah_Negar.Infrastructure.Database.Checksums;
using Rah_Negar.Infrastructure.Database.Migrations;
using Rah_Negar.Infrastructure.Database.Migrations.Drafts;
using Rah_Negar.Infrastructure.Security;

namespace Rah_Negar.Infrastructure.Database.Readiness;

public sealed class SystemTemporaryRehearsalWorkspaceFactory : IIsolatedRehearsalWorkspaceFactory
{
    public async Task<IIsolatedRehearsalWorkspace> CreateAsync(string explicitVerifiedBackupPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(explicitVerifiedBackupPath);
        string source = Path.GetFullPath(explicitVerifiedBackupPath);
        if (!File.Exists(source)) throw new FileNotFoundException("Verified backup is missing.", source);
        string root = Path.Combine(Path.GetTempPath(), "RahNegar.MigrationRehearsal");
        string directory = Path.Combine(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string copy = Path.Combine(directory, "rehearsal.sqlite");
        await using (FileStream input = new(source, FileMode.Open, FileAccess.Read, FileShare.Read,
                         81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
        await using (FileStream output = new(copy, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                         81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
            await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        return new Workspace(root, directory, copy);
    }

    private sealed class Workspace : IIsolatedRehearsalWorkspace
    {
        private readonly string _root;
        private readonly string _directory;

        public Workspace(string root, string directory, string databaseCopyPath)
        {
            _root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            _directory = Path.GetFullPath(directory);
            DatabaseCopyPath = databaseCopyPath;
        }

        public string DatabaseCopyPath { get; }

        public ValueTask DisposeAsync()
        {
            SqliteConnection.ClearAllPools();
            string target = Path.GetFullPath(_directory);
            if (target.StartsWith(_root, StringComparison.OrdinalIgnoreCase) &&
                !StringComparer.OrdinalIgnoreCase.Equals(target.TrimEnd(Path.DirectorySeparatorChar),
                    _root.TrimEnd(Path.DirectorySeparatorChar)) && Directory.Exists(target))
                Directory.Delete(target, recursive: true);
            return ValueTask.CompletedTask;
        }
    }
}

public sealed class MigrationRehearsalService : IMigrationRehearsalService
{
    private readonly IRestoreValidationService _restoreValidation;
    private readonly IIsolatedRehearsalWorkspaceFactory _workspaces;
    private readonly IReadOnlyDatabasePreflightAnalyzer _preflight;
    private readonly IDatabaseStructuralFingerprintService _fingerprints;
    private readonly IChecksumService _checksums;
    private readonly IEsdAdjustmentReconciliationPolicy _esdPolicy;
    private readonly IClock _clock;

    public MigrationRehearsalService(IRestoreValidationService restoreValidation,
        IIsolatedRehearsalWorkspaceFactory workspaces, IReadOnlyDatabasePreflightAnalyzer preflight,
        IDatabaseStructuralFingerprintService fingerprints, IChecksumService checksums,
        IEsdAdjustmentReconciliationPolicy esdPolicy, IClock clock)
    {
        _restoreValidation = restoreValidation ?? throw new ArgumentNullException(nameof(restoreValidation));
        _workspaces = workspaces ?? throw new ArgumentNullException(nameof(workspaces));
        _preflight = preflight ?? throw new ArgumentNullException(nameof(preflight));
        _fingerprints = fingerprints ?? throw new ArgumentNullException(nameof(fingerprints));
        _checksums = checksums ?? throw new ArgumentNullException(nameof(checksums));
        _esdPolicy = esdPolicy ?? throw new ArgumentNullException(nameof(esdPolicy));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<MigrationRehearsalResult> RehearseAsync(
        DatabaseBackupVerificationResult verifiedBackup, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(verifiedBackup);
        if (!verifiedBackup.IsVerified || string.IsNullOrWhiteSpace(verifiedBackup.BackupSha256))
            return Fail(MigrationRehearsalFailure.BackupNotVerified, "VerifiedBackupRequired");
        RestoreValidationResult restored = await _restoreValidation.ValidateAsync(verifiedBackup.BackupPath,
            verifiedBackup.BackupSha256, cancellationToken).ConfigureAwait(false);
        if (!restored.IsValid)
            return Fail(MigrationRehearsalFailure.BackupNotVerified, "BackupRevalidationFailed");
        if (restored.MigrationState is not (MigrationHistoryClassification.CleanLegacyBaseline or
            MigrationHistoryClassification.CleanUnifiedTarget))
            return Fail(MigrationRehearsalFailure.UnsupportedMigrationState, "MigrationStateRequiresAdoption");

        string originalHash = await ReadinessFileHash.ComputeSha256Async(verifiedBackup.BackupPath, cancellationToken)
            .ConfigureAwait(false);
        try
        {
            await using IIsolatedRehearsalWorkspace workspace = await _workspaces.CreateAsync(
                verifiedBackup.BackupPath, cancellationToken).ConfigureAwait(false);
            DatabaseStructuralFingerprint before = await _fingerprints.CaptureAsync(workspace.DatabaseCopyPath,
                cancellationToken).ConfigureAwait(false);
            var factory = new SqliteConnectionFactory(new SqliteDatabaseOptions
            {
                DataSource = workspace.DatabaseCopyPath,
                Pooling = false,
                Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWrite
            });
            var runner = new MigrationRunner(new SqliteTransactionManager(factory),
                new MigrationChecksumValidator(_checksums));
            IReadOnlyList<IDatabaseMigration> chain = UnifiedTargetMigrationChain.Create(_checksums);
            MigrationRunResult first = await runner.RunPendingAsync(chain, cancellationToken).ConfigureAwait(false);
            MigrationRunResult second = await runner.RunPendingAsync(chain, cancellationToken).ConfigureAwait(false);
            bool idempotent = second.AppliedMigrationIds.Count == 0 &&
                second.FinalVersion == UnifiedTargetMigrationChain.FinalVersion;
            DatabasePreflightResult afterPreflight = await _preflight.AnalyzeAsync(workspace.DatabaseCopyPath,
                IntegrityCheckStrategy.FullIntegrityCheck, cancellationToken).ConfigureAwait(false);
            if (!afterPreflight.Succeeded || !afterPreflight.IntegrityPassed ||
                afterPreflight.ForeignKeyViolations.Count > 0)
                return Fail(MigrationRehearsalFailure.IntegrityFailed, "RehearsalIntegrityFailed",
                    first.InitialVersion, first.FinalVersion, first.AppliedMigrationIds, idempotent);

            var legacyReader = new SQLiteLegacyEsdValueReader(factory, _esdPolicy);
            var targetReader = new SQLiteTargetEsdProvisioningStore(factory);
            var reconciliation = new LegacyEsdReconciliationService(legacyReader, targetReader,
                new InactivePreCutoverEsdAuthorityProvider());
            EsdReconciliationResult esd = await reconciliation.InspectAsync("phase7.9-rehearsal",
                _clock.UtcNow.ToUniversalTime(), cancellationToken).ConfigureAwait(false);
            bool esdConflict = esd.State is EsdReconciliationState.Conflict or
                EsdReconciliationState.TargetAlreadyProvisionedDifferentValue;
            DatabaseStructuralFingerprint after = await _fingerprints.CaptureAsync(workspace.DatabaseCopyPath,
                cancellationToken).ConfigureAwait(false);
            bool ledgerProgress = first.FinalVersion == UnifiedTargetMigrationChain.FinalVersion && idempotent;
            PreservationVerificationResult preservation = DatabasePreservationVerifier.Compare(before, after,
                ledgerProgress);
            string originalAfter = await ReadinessFileHash.ComputeSha256Async(verifiedBackup.BackupPath,
                cancellationToken).ConfigureAwait(false);
            bool originalUnchanged = StringComparer.Ordinal.Equals(originalHash, originalAfter);
            if (esdConflict)
                return new(false, MigrationRehearsalFailure.EsdConflict, first.InitialVersion,
                    first.FinalVersion, first.AppliedMigrationIds, idempotent, originalUnchanged,
                    preservation, esd.State, esd.AuthorityMode, ["EsdConflictRequiresManualResolution"]);
            if (!preservation.Passed || !originalUnchanged)
                return new(false, MigrationRehearsalFailure.PreservationFailed, first.InitialVersion,
                    first.FinalVersion, first.AppliedMigrationIds, idempotent, originalUnchanged,
                    preservation, esd.State, esd.AuthorityMode, ["PreservationVerificationFailed"]);
            return new(true, MigrationRehearsalFailure.None, first.InitialVersion, first.FinalVersion,
                first.AppliedMigrationIds, idempotent, true, preservation, esd.State,
                esd.AuthorityMode, Array.Empty<string>());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            return Fail(MigrationRehearsalFailure.MigrationFailed, "MigrationRehearsalFailed");
        }
    }

    private static MigrationRehearsalResult Fail(MigrationRehearsalFailure failure, string error,
        int initialVersion = 0, int finalVersion = 0, IReadOnlyList<string>? applied = null,
        bool idempotent = false) =>
        new(false, failure, initialVersion, finalVersion, applied ?? Array.Empty<string>(), idempotent,
            false, null, EsdReconciliationState.Failed, EsdAuthorityMode.LegacyAuthoritative, [error]);
}
