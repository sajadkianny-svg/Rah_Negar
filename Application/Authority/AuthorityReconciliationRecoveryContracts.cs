using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using Rah_Negar.Foundation.Application.Database.Readiness;

namespace Rah_Negar.Foundation.Application.Authority;

public enum ReconciliationComponentCategory
{
    Schema, MigrationLedger, ProfileIdentity, ProfileVersion, UnitBoundary,
    OperationalRecords, EventsRuntime, FinalizedSnapshots, CanonicalEvidence
}

public sealed record ReconciliationComponentSnapshot(
    ReconciliationComponentCategory Category,
    bool Available,
    string? LegacyValue,
    string? TargetValue,
    string? EvidenceReference = null)
{
    public bool Matches => Available && LegacyValue is not null &&
        StringComparer.Ordinal.Equals(LegacyValue, TargetValue);
}

public sealed record StructuredReconciliationResult(
    ReconciliationStatus Status,
    IReadOnlyList<ReconciliationComponentSnapshot> Components,
    IReadOnlyList<string> MismatchReasons,
    IReadOnlyList<string> UnavailableEvidence,
    bool MutatedAuthority = false,
    bool MutatedRouting = false,
    bool MutatedData = false)
{
    public bool IsReadOnly => !MutatedAuthority && !MutatedRouting && !MutatedData;
}

/// <summary>Compares supplied evidence only. It never opens a writable connection or repairs data.</summary>
public sealed class LegacyTargetReconciliationEvaluator
{
    public StructuredReconciliationResult Evaluate(
        IEnumerable<ReconciliationComponentSnapshot> components)
    {
        var items = components?.ToArray() ?? throw new ArgumentNullException(nameof(components));
        var mismatches = items.Where(x => x.Available && !x.Matches)
            .Select(x => $"{x.Category}:legacy={x.LegacyValue ?? "missing"};target={x.TargetValue ?? "missing"}")
            .ToArray();
        var unavailable = items.Where(x => !x.Available)
            .Select(x => $"{x.Category}:{x.EvidenceReference ?? "evidence-unavailable"}")
            .ToArray();
        ReconciliationStatus status = unavailable.Length > 0 ? ReconciliationStatus.Blocked :
            items.Length == 0 ? ReconciliationStatus.NotEvaluated :
            mismatches.Length > 0 ? ReconciliationStatus.Mismatched : ReconciliationStatus.Matched;
        return new(status, new ReadOnlyCollection<ReconciliationComponentSnapshot>(items), mismatches, unavailable);
    }

    public StructuredReconciliationResult Evaluate(
        ReconciliationDatabaseSnapshot legacy, ReconciliationDatabaseSnapshot target)
    {
        var components = new List<ReconciliationComponentSnapshot>();
        Add(components, ReconciliationComponentCategory.Schema, legacy.SchemaFingerprint, target.SchemaFingerprint);
        Add(components, ReconciliationComponentCategory.MigrationLedger, legacy.MigrationLedgerFingerprint, target.MigrationLedgerFingerprint);
        Add(components, ReconciliationComponentCategory.ProfileIdentity, legacy.ProfileIdentity, target.ProfileIdentity);
        Add(components, ReconciliationComponentCategory.ProfileVersion, legacy.ProfileVersion, target.ProfileVersion);
        Add(components, ReconciliationComponentCategory.UnitBoundary, legacy.UnitBoundary, target.UnitBoundary);
        Add(components, ReconciliationComponentCategory.OperationalRecords, legacy.OperationalRecords, target.OperationalRecords);
        Add(components, ReconciliationComponentCategory.EventsRuntime, legacy.EventsRuntime, target.EventsRuntime);
        Add(components, ReconciliationComponentCategory.FinalizedSnapshots, legacy.FinalizedSnapshots, target.FinalizedSnapshots);
        Add(components, ReconciliationComponentCategory.CanonicalEvidence, legacy.CanonicalEvidence, target.CanonicalEvidence);
        return Evaluate(components);
    }

    private static void Add(List<ReconciliationComponentSnapshot> list,
        ReconciliationComponentCategory category, string? legacy, string? target)
    {
        bool available = legacy is not null && target is not null;
        list.Add(new(category, available, legacy, target, available ? null : "required-evidence-unavailable"));
    }
}

public sealed record ReconciliationDatabaseSnapshot(
    string? SchemaFingerprint, string? MigrationLedgerFingerprint, string? ProfileIdentity,
    string? ProfileVersion, string? UnitBoundary, string? OperationalRecords,
    string? EventsRuntime, string? FinalizedSnapshots, string? CanonicalEvidence);

public interface IReadOnlyReconciliationSource
{
    Task<ReconciliationDatabaseSnapshot> ReadAsync(string databasePath, CancellationToken cancellationToken = default);
}

/// <summary>Read-only adapter over the existing preflight/fingerprint infrastructure.</summary>
public sealed class PreflightReconciliationSource : IReadOnlyReconciliationSource
{
    private readonly IReadOnlyDatabasePreflightAnalyzer _preflight;
    private readonly IDatabaseStructuralFingerprintService _fingerprints;

    public PreflightReconciliationSource(IReadOnlyDatabasePreflightAnalyzer preflight,
        IDatabaseStructuralFingerprintService fingerprints)
    {
        _preflight = preflight ?? throw new ArgumentNullException(nameof(preflight));
        _fingerprints = fingerprints ?? throw new ArgumentNullException(nameof(fingerprints));
    }

    public async Task<ReconciliationDatabaseSnapshot> ReadAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        DatabasePreflightResult p = await _preflight.AnalyzeAsync(databasePath,
            IntegrityCheckStrategy.FullIntegrityCheck, cancellationToken).ConfigureAwait(false);
        if (!p.Succeeded || !p.ReadOnlyConnectionEnforced || !p.IntegrityPassed || p.ForeignKeyViolations.Count != 0)
            throw new InvalidOperationException("reconciliation-source-unavailable");
        DatabaseStructuralFingerprint f = await _fingerprints.CaptureAsync(databasePath, cancellationToken).ConfigureAwait(false);
        string schema = Fingerprint(p.SchemaObjects.Select(x => $"{x.Type}|{x.Name}|{x.TableName}|{x.DefinitionSha256}"));
        string ledger = Fingerprint(new[] { p.MigrationLedger.CurrentVersion?.ToString() ?? "missing" }
            .Concat(p.MigrationLedger.Entries.Select(x => $"{x.MigrationId}|{x.FromVersion}|{x.ToVersion}|{x.Checksum}")));
        string profile = Value(p.RowCounts, "Stations") ?? Value(p.RowCounts, "app_settings") ?? "none";
        string units = Value(p.RowCounts, "Units") ?? Value(p.RowCounts, "unit_runtime_base") ?? "unavailable";
        string operational = Fingerprint(p.RowCounts.Where(x => x.Key.Contains("data", StringComparison.OrdinalIgnoreCase) ||
            x.Key.Contains("unique", StringComparison.OrdinalIgnoreCase)).Select(x => $"{x.Key}|{x.Value}"));
        string events = Fingerprint(p.RowCounts.Where(x => x.Key.Contains("event", StringComparison.OrdinalIgnoreCase) ||
            x.Key.Contains("runtime", StringComparison.OrdinalIgnoreCase)).Select(x => $"{x.Key}|{x.Value}"));
        string finalized = Fingerprint(p.FinalizedEvidence.SnapshotHashes.Concat(p.FinalizedEvidence.LockHashes)
            .Select(x => $"{x.Key}|{x.Value}"));
        return new(schema, ledger, profile, p.SchemaVersion.ToString(), units, operational, events, finalized, f.Sha256);
    }

    private static string? Value(IReadOnlyDictionary<string, long> values, string key) =>
        values.TryGetValue(key, out long value) ? value.ToString() : null;
    private static string Fingerprint(IEnumerable<string> values) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", values.OrderBy(x => x, StringComparer.Ordinal)))));
}

public sealed record ExecutableReconciliationResult(
    ReconciliationStatus Status, StructuredReconciliationResult? Evidence, IReadOnlyList<string> Reasons)
{
    public bool IsReadOnly => Evidence?.IsReadOnly ?? true;
}

public sealed class ExecutableReconciliationService
{
    private readonly IReadOnlyReconciliationSource _source;
    private readonly LegacyTargetReconciliationEvaluator _evaluator = new();
    public ExecutableReconciliationService(IReadOnlyReconciliationSource source) => _source = source ?? throw new ArgumentNullException(nameof(source));

    public async Task<ExecutableReconciliationResult> EvaluateAsync(string legacyPath, string targetPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ReconciliationDatabaseSnapshot legacy = await _source.ReadAsync(legacyPath, cancellationToken).ConfigureAwait(false);
            ReconciliationDatabaseSnapshot target = await _source.ReadAsync(targetPath, cancellationToken).ConfigureAwait(false);
            StructuredReconciliationResult result = _evaluator.Evaluate(legacy, target);
            return new(result.Status, result, result.MismatchReasons.Concat(result.UnavailableEvidence).ToArray());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex) { return new(ReconciliationStatus.Blocked, null, [$"reconciliation-source-unavailable:{ex.GetType().Name}"]); }
    }
}

public enum DivergenceStatus { Synchronized, DeltaPending, Diverged, NotEvaluated, Blocked }
public sealed record DivergenceEvidence(bool Available, bool NewLegacyWrites, bool TargetBehind,
    bool UnresolvedDelta, bool ConflictingValues, string? Reason = null);
public sealed record DivergenceResult(DivergenceStatus Status, IReadOnlyList<string> Reasons);

public sealed class ReadOnlyDivergenceEvaluator
{
    public DivergenceResult Evaluate(DivergenceEvidence evidence)
    {
        if (!evidence.Available) return new(DivergenceStatus.Blocked, [evidence.Reason ?? "divergence-evidence-unavailable"]);
        if (evidence.ConflictingValues) return new(DivergenceStatus.Diverged, ["conflicting-legacy-target-values"]);
        if (evidence.UnresolvedDelta || evidence.NewLegacyWrites || evidence.TargetBehind)
            return new(DivergenceStatus.DeltaPending, ["final-synchronization-required"]);
        return new(DivergenceStatus.Synchronized, ["legacy-target-synchronized"]);
    }
}

public enum AbortResultStatus { Aborted, AlreadyAborted, Rejected, RecoveryRequired }
public sealed record AbortResult(AbortResultStatus Status, string Reason, bool LegacyRemainsAuthoritative,
    bool TargetRoutingEnabled, AuthorityTransitionRecord? Transition);

public sealed class AuthorityAbortService
{
    private readonly IAuthorityStateStore _authority;
    private readonly ITransitionStateStore _transitions;
    private readonly IAuthorityAuditSink _audit;
    public AuthorityAbortService(IAuthorityStateStore authority, ITransitionStateStore transitions, IAuthorityAuditSink audit) =>
        (_authority, _transitions, _audit) = (authority ?? throw new ArgumentNullException(nameof(authority)),
            transitions ?? throw new ArgumentNullException(nameof(transitions)), audit ?? throw new ArgumentNullException(nameof(audit)));

    public async Task<AbortResult> AbortAsync(string correlationId, long generation, CancellationToken cancellationToken = default)
    {
        AuthorityLoadResult current = await _authority.LoadAsync(cancellationToken).ConfigureAwait(false);
        TransitionLoadResult loaded = await _transitions.LoadAsync(cancellationToken).ConfigureAwait(false);
        AuthorityTransitionRecord? transition = loaded.Record;
        if (!current.IsOperationallySafe || transition is null)
            return new(AbortResultStatus.RecoveryRequired, "abort-state-unavailable", false, false, transition);
        if (!StringComparer.Ordinal.Equals(transition.TransitionId, correlationId) || transition.Generation != generation)
            return new(AbortResultStatus.Rejected, "abort-correlation-or-generation-mismatch", current.Record.LegacyAuthoritative, current.Record.TargetRoutingEnabled, transition);
        if (transition.Lifecycle == TransitionLifecycle.Aborted)
            return new(AbortResultStatus.AlreadyAborted, "abort-idempotent-retry", true, false, transition);
        if (transition.Lifecycle != TransitionLifecycle.Prepared || current.Record.State != AuthorityState.LegacyAuthoritative ||
            current.Record.AuthorityEpoch >= generation)
            return new(AbortResultStatus.Rejected, "abort-only-valid-before-commit", current.Record.LegacyAuthoritative, current.Record.TargetRoutingEnabled, transition);
        try
        {
            AuthorityTransitionRecord aborted = transition with { Lifecycle = TransitionLifecycle.Aborted, RecordedAtUtc = DateTimeOffset.UtcNow };
            await _transitions.SaveAsync(aborted, cancellationToken).ConfigureAwait(false);
            await _audit.WriteAsync(Audit(current.Record, correlationId, AuthorityAuditAction.Abort, "ABORTED", "pre-commit-abort", generation), cancellationToken).ConfigureAwait(false);
            return new(AbortResultStatus.Aborted, "abort-complete", true, false, aborted);
        }
        catch
        {
            await TryEnterRecoveryAsync(current.Record, correlationId, generation, cancellationToken).ConfigureAwait(false);
            return new(AbortResultStatus.RecoveryRequired, "abort-persistence-or-audit-failed", false, false, transition);
        }
    }

    private async Task TryEnterRecoveryAsync(AuthorityStateRecord current, string correlationId, long generation, CancellationToken token)
    {
        try { await _authority.SaveAsync(current with { State = AuthorityState.RecoveryRequired, LegacyAuthoritative = false,
            TargetAuthoritative = false, TargetRoutingEnabled = false, CorrelationId = correlationId,
            Reason = "abort-outcome-unknown", AuthorityEpoch = Math.Max(current.AuthorityEpoch, generation),
            Revision = current.Revision + 1, RecordedAtUtc = DateTimeOffset.UtcNow }, token).ConfigureAwait(false); }
        catch { /* startup validation remains fail-closed when persistence is unavailable */ }
    }

    private static AuthorityAuditEntry Audit(AuthorityStateRecord current, string correlationId,
        AuthorityAuditAction action, string result, string reason, long generation) =>
        new(correlationId, DateTimeOffset.UtcNow, current.DeploymentScope, current.StationScope, "9.6D3",
            current.State, AuthorityState.ActivationPreparedNotExecuted, action, result, reason, null, generation);
}

public enum RollbackEligibilityStatus { RollbackEligible, RollbackNotSafe, RecoveryRequired, NotEvaluated }
public sealed record RollbackSafetyEvidence(
    bool Available, bool TargetAuthoritativeWrites, bool TargetLegacyDiverged,
    ReconciliationStatus Reconciliation, bool FinalizedReportsAfterHandoff,
    bool EventsAfterHandoff, bool AuditComplete, bool VerifiedBackupReceipt,
    bool RestoreReady, AuthorityState CurrentState, long AuthorityEpoch,
    long TransitionGeneration, string? Reason = null);
public sealed record RollbackEligibilityResult(RollbackEligibilityStatus Status, IReadOnlyList<string> Reasons)
{
    public bool IsEligible => Status == RollbackEligibilityStatus.RollbackEligible;
}

public sealed class RollbackEligibilityEvaluator
{
    public RollbackEligibilityResult Evaluate(RollbackSafetyEvidence e)
    {
        if (!e.Available) return new(RollbackEligibilityStatus.NotEvaluated, [e.Reason ?? "rollback-evidence-unavailable"]);
        if (e.CurrentState == AuthorityState.RecoveryRequired || e.AuthorityEpoch != e.TransitionGeneration)
            return new(RollbackEligibilityStatus.RecoveryRequired, ["authority-generation-or-recovery-state-invalid"]);
        var unsafeReasons = new List<string>();
        if (e.TargetAuthoritativeWrites) unsafeReasons.Add("target-authoritative-writes-present");
        if (e.TargetLegacyDiverged || e.Reconciliation is ReconciliationStatus.Mismatched or ReconciliationStatus.Blocked)
            unsafeReasons.Add("target-legacy-divergence-or-reconciliation-failure");
        if (e.FinalizedReportsAfterHandoff) unsafeReasons.Add("finalized-reports-after-handoff");
        if (e.EventsAfterHandoff) unsafeReasons.Add("events-runtime-after-handoff");
        if (!e.AuditComplete) unsafeReasons.Add("audit-incomplete");
        if (!e.VerifiedBackupReceipt) unsafeReasons.Add("verified-backup-receipt-missing");
        if (!e.RestoreReady) unsafeReasons.Add("restore-not-ready");
        return unsafeReasons.Count > 0 ? new(RollbackEligibilityStatus.RollbackNotSafe, unsafeReasons) :
            new(RollbackEligibilityStatus.RollbackEligible, ["rollback-evidence-complete"]);
    }
}

public enum RehearsalContext { Qualification, Rehearsal, Production }
public enum RehearsalRollbackStatus { Succeeded, Rejected, RecoveryRequired }
public sealed record RehearsalRollbackResult(RehearsalRollbackStatus Status, string Reason, AuthorityStateRecord Authority);

/// <summary>Only accepts isolated contexts. It cannot be composed into production startup or routing.</summary>
public sealed class RehearsalRollbackService
{
    private readonly IAuthorityStateStore _authority;
    private readonly ITransitionStateStore _transitions;
    private readonly IAuthorityAuditSink _audit;
    private readonly RollbackEligibilityEvaluator _eligibility = new();
    public RehearsalRollbackService(IAuthorityStateStore authority, ITransitionStateStore transitions, IAuthorityAuditSink audit) =>
        (_authority, _transitions, _audit) = (authority, transitions, audit);

    public async Task<RehearsalRollbackResult> RollbackAsync(RehearsalContext context, string correlationId,
        long generation, RollbackSafetyEvidence evidence, CancellationToken cancellationToken = default)
    {
        AuthorityLoadResult current = await _authority.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (context == RehearsalContext.Production)
            return new(RehearsalRollbackStatus.Rejected, "production-rollback-not-permitted", current.Record);
        RollbackEligibilityResult eligible = _eligibility.Evaluate(evidence);
        if (!eligible.IsEligible) return new(RehearsalRollbackStatus.Rejected, string.Join(';', eligible.Reasons), current.Record);
        TransitionLoadResult loaded = await _transitions.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (loaded.Record is null || loaded.Record.TransitionId != correlationId || loaded.Record.Generation != generation)
            return new(RehearsalRollbackStatus.Rejected, "rollback-correlation-or-generation-mismatch", current.Record);
        try
        {
            AuthorityStateRecord restored = AuthorityStateRecord.Legacy(current.Record.DeploymentScope, current.Record.StationScope) with
            { Revision = current.Record.Revision + 1, AuthorityEpoch = generation, CorrelationId = correlationId, Reason = "rehearsal-rollback" };
            await _authority.SaveAsync(restored, cancellationToken).ConfigureAwait(false);
            await _transitions.SaveAsync(loaded.Record with { Lifecycle = TransitionLifecycle.Aborted, RecordedAtUtc = DateTimeOffset.UtcNow }, cancellationToken).ConfigureAwait(false);
            await _audit.WriteAsync(new(correlationId, DateTimeOffset.UtcNow, restored.DeploymentScope, restored.StationScope,
                "9.6D3", current.Record.State, AuthorityState.LegacyAuthoritative, AuthorityAuditAction.RollbackSucceeded,
                "SUCCEEDED", "isolated-rehearsal-rollback", null, generation), cancellationToken).ConfigureAwait(false);
            return new(RehearsalRollbackStatus.Succeeded, "isolated-rehearsal-rollback-complete", restored);
        }
        catch
        {
            AuthorityStateRecord recovery = current.Record with { State = AuthorityState.RecoveryRequired,
                LegacyAuthoritative = false, TargetAuthoritative = false, TargetRoutingEnabled = false,
                CorrelationId = correlationId, Reason = "rollback-outcome-unknown", Revision = current.Record.Revision + 1,
                RecordedAtUtc = DateTimeOffset.UtcNow };
            try { await _authority.SaveAsync(recovery, cancellationToken).ConfigureAwait(false); } catch { }
            return new(RehearsalRollbackStatus.RecoveryRequired, "rollback-persistence-or-audit-failed", recovery);
        }
    }
}

public sealed record VerifiedBackupEvidence(
    bool IsVerified, string? SourcePath, string? BackupPath, string? SourceSha256,
    string? BackupSha256, string JournalMode, bool IntegrityPassed, bool ForeignKeysPassed,
    bool WalReceiptPresent, string DeploymentScope, string SchemaVersion, string CorrelationId,
    DateTimeOffset CreatedAtUtc, IReadOnlyList<string> Reasons)
{
    public bool IsUsable => IsVerified && IntegrityPassed && ForeignKeysPassed &&
        !string.IsNullOrWhiteSpace(BackupSha256) && !string.IsNullOrWhiteSpace(CorrelationId);
}

public static class VerifiedBackupEvidenceFactory
{
    public static VerifiedBackupEvidence From(
        ManagedSqliteBackupResult result, string deploymentScope, string schemaVersion)
    {
        ArgumentNullException.ThrowIfNull(result);
        SqliteBackupReceipt receipt = result.Receipt;
        bool walReceiptPresent = receipt.SourceSidecars.Any(x => x.Suffix == "-wal");
        var reasons = result.Errors.Concat(result.Verification.Errors).ToArray();
        return new(result.Succeeded && result.Verification.IsVerified, receipt.SourcePath, receipt.BackupPath,
            receipt.SourceSha256, receipt.BackupSha256, receipt.JournalMode, receipt.IntegrityPassed,
            receipt.ForeignKeysPassed, walReceiptPresent, deploymentScope, schemaVersion,
            receipt.CorrelationId, receipt.CreatedAtUtc, reasons);
    }
}

public static class RecoveryOperatorMessage
{
    public static string Persian(string classification, IReadOnlyList<string> issues) =>
        $"وضعیت مرجع داده‌ها نیازمند بازیابی است. برنامه وارد جریان عادی ورود نمی‌شود.\n" +
        $"شناسه تشخیصی: {Safe(classification)}\n" +
        $"علت: {Safe(issues.FirstOrDefault() ?? "recovery-required")}\n" +
        "اقدام مجاز: خروج و اجرای دوباره اعتبارسنجی.";

    private static string Safe(string value) => new(value.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or ':' or ';' or ' ').ToArray());
}

public enum RehearsalTransitionStatus { Succeeded, Rejected, RecoveryRequired }

public sealed record RehearsalTransitionRequest(
    RehearsalContext Context, string CorrelationId, string DeploymentScope, string StationScope,
    string ApplicationVersion, long ExpectedGeneration, ReconciliationStatus Reconciliation,
    DivergenceStatus Divergence, VerifiedBackupEvidence Backup);

public sealed record RehearsalTransitionResult(
    RehearsalTransitionStatus Status, string Reason, AuthorityStateRecord Authority,
    AuthorityTransitionRecord? Transition, bool TargetRoutingEligible, IReadOnlyList<string> AuditActions);

/// <summary>
/// Qualification-only authority handoff. Production is rejected before any store is read or written.
/// This service is intentionally not registered by Program.cs and has no production activation path.
/// </summary>
public sealed class IsolatedRehearsalTransitionService
{
    private readonly IAuthorityStateStore _authority;
    private readonly ITransitionStateStore _transitions;
    private readonly IAuthorityAuditSink _audit;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public IsolatedRehearsalTransitionService(IAuthorityStateStore authority,
        ITransitionStateStore transitions, IAuthorityAuditSink audit) =>
        (_authority, _transitions, _audit) = (authority ?? throw new ArgumentNullException(nameof(authority)),
            transitions ?? throw new ArgumentNullException(nameof(transitions)),
            audit ?? throw new ArgumentNullException(nameof(audit)));

    public async Task<RehearsalTransitionResult> ExecuteAsync(RehearsalTransitionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Context == RehearsalContext.Production)
            return Rejected("production-transition-not-permitted");
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AuthorityLoadResult current = await _authority.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (!current.IsOperationallySafe) return Recovery(current.Record, "authority-state-unavailable");
            if (request.Reconciliation != ReconciliationStatus.Matched || request.Divergence != DivergenceStatus.Synchronized)
                return Rejected(current.Record, "reconciliation-or-divergence-not-safe");
            if (!StringComparer.Ordinal.Equals(current.Record.DeploymentScope, request.DeploymentScope) ||
                !StringComparer.Ordinal.Equals(current.Record.StationScope, request.StationScope))
                return Rejected(current.Record, "transition-scope-mismatch");
            if (!request.Backup.IsUsable || !StringComparer.Ordinal.Equals(request.Backup.DeploymentScope, request.DeploymentScope) ||
                !StringComparer.Ordinal.Equals(request.Backup.CorrelationId, request.CorrelationId))
                return Rejected(current.Record, "verified-backup-evidence-rejected");
            if (current.Record.State != AuthorityState.LegacyAuthoritative ||
                request.ExpectedGeneration != current.Record.AuthorityEpoch + 1 || string.IsNullOrWhiteSpace(request.CorrelationId) ||
                string.IsNullOrWhiteSpace(request.ApplicationVersion) || request.ApplicationVersion.Any(char.IsWhiteSpace))
                return Rejected(current.Record, "stale-or-invalid-transition-request");

            var transition = new AuthorityTransitionRecord(request.CorrelationId, request.ExpectedGeneration,
                AuthorityState.LegacyAuthoritative, AuthorityState.TargetAuthoritative,
                TransitionLifecycle.Committing, DateTimeOffset.UtcNow, request.DeploymentScope, request.StationScope);
            await _transitions.SaveAsync(transition, cancellationToken).ConfigureAwait(false);
            await AuditAsync(current.Record, request, AuthorityAuditAction.CommitAttempt, "ATTEMPTED", "rehearsal-commit-started", cancellationToken).ConfigureAwait(false);

            AuthorityStateRecord target = current.Record with
            {
                Revision = current.Record.Revision + 1, AuthorityEpoch = request.ExpectedGeneration,
                State = AuthorityState.TargetAuthoritative, LegacyAuthoritative = false,
                TargetAuthoritative = true, TargetRoutingEnabled = false,
                CorrelationId = request.CorrelationId, Reason = "isolated-rehearsal-commit", RecordedAtUtc = DateTimeOffset.UtcNow
            };
            await _authority.SaveAsync(target, cancellationToken).ConfigureAwait(false);
            await _transitions.SaveAsync(transition with { Lifecycle = TransitionLifecycle.Committed, RecordedAtUtc = DateTimeOffset.UtcNow }, cancellationToken).ConfigureAwait(false);
            await AuditAsync(target, request, AuthorityAuditAction.CommitSucceeded, "COMMITTED", "isolated-rehearsal-commit-complete", cancellationToken).ConfigureAwait(false);
            return new(RehearsalTransitionStatus.Succeeded, "isolated-rehearsal-transition-complete", target,
                transition with { Lifecycle = TransitionLifecycle.Committed },
                AuthorityRoutingGuard.IsTargetAuthorityReadyForRouting(target),
                ["CommitAttempt", "CommitSucceeded"]);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            AuthorityLoadResult current = await _authority.LoadAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                AuthorityStateRecord recovery = current.Record with { State = AuthorityState.RecoveryRequired,
                    LegacyAuthoritative = false, TargetAuthoritative = false, TargetRoutingEnabled = false,
                    CorrelationId = request.CorrelationId, Reason = "rehearsal-commit-outcome-unknown",
                    Revision = current.Record.Revision + 1, AuthorityEpoch = Math.Max(current.Record.AuthorityEpoch, request.ExpectedGeneration), RecordedAtUtc = DateTimeOffset.UtcNow };
                await _authority.SaveAsync(recovery, CancellationToken.None).ConfigureAwait(false);
                return Recovery(recovery, "rehearsal-commit-failed-recovery-required");
            }
            catch { return Recovery(current.Record, "rehearsal-commit-failed-recovery-persistence-failed"); }
        }
        finally { _mutex.Release(); }
    }

    private async Task AuditAsync(AuthorityStateRecord current, RehearsalTransitionRequest request,
        AuthorityAuditAction action, string result, string reason, CancellationToken token) =>
        await _audit.WriteAsync(new(request.CorrelationId, DateTimeOffset.UtcNow, request.DeploymentScope,
            request.StationScope, request.ApplicationVersion, current.State, AuthorityState.TargetAuthoritative,
            action, result, reason, request.Backup.BackupPath, request.ExpectedGeneration), token).ConfigureAwait(false);

    private static RehearsalTransitionResult Rejected(string reason) =>
        new(RehearsalTransitionStatus.Rejected, reason, AuthorityStateRecord.Legacy("qualification", "all"), null, false, []);
    private static RehearsalTransitionResult Rejected(AuthorityStateRecord authority, string reason) =>
        new(RehearsalTransitionStatus.Rejected, reason, authority, null, false, []);
    private static RehearsalTransitionResult Recovery(AuthorityStateRecord authority, string reason) =>
        new(RehearsalTransitionStatus.RecoveryRequired, reason, authority, null, false, ["RecoveryRequired"]);
}
