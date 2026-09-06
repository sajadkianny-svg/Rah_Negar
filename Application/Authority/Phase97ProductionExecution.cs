using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Rah_Negar.Foundation.Application.Security;
using Rah_Negar.Foundation.Time;

namespace Rah_Negar.Foundation.Application.Authority;

// Phase 9.7 execution contracts are deliberately separate from the existing
// preparation/rehearsal contracts. Nothing in this file is registered by
// production startup or exposed as a UI action.

public enum ProductionExecutionAction { Activate, Rollback }

public sealed record ProductionExecutionScope(
    string DeploymentScope, string StationScope, string ProfileScope)
{
    public bool IsComplete => IsSafe(DeploymentScope) && IsSafe(StationScope) && IsSafe(ProfileScope);
    private static bool IsSafe(string? value) => !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 160 && value.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.' or ':');
}

public sealed record ProductionAuthorityGeneration(long Generation, long Epoch)
{
    public bool IsValid => Generation >= 0 && Epoch >= 0 && Generation == Epoch;
}

public sealed record ProductionPrerequisiteEvaluationReceipt(
    string ReceiptId, bool IsValid, DateTimeOffset EvaluatedAtUtc, string EvidenceReference)
{
    public bool IsComplete => !string.IsNullOrWhiteSpace(ReceiptId) && IsValid &&
        EvaluatedAtUtc.Offset == TimeSpan.Zero && !string.IsNullOrWhiteSpace(EvidenceReference);
}

public sealed record GovernanceAuthorizationReceipt(
    string DecisionReference, string ProjectOwnerReference, bool ExplicitlyAuthorized,
    DateTimeOffset IssuedAtUtc, DateTimeOffset ExpiresAtUtc)
{
    public bool IsComplete(DateTimeOffset nowUtc) => ExplicitlyAuthorized &&
        !string.IsNullOrWhiteSpace(DecisionReference) && !string.IsNullOrWhiteSpace(ProjectOwnerReference) &&
        IssuedAtUtc.Offset == TimeSpan.Zero && ExpiresAtUtc.Offset == TimeSpan.Zero &&
        IssuedAtUtc <= nowUtc && nowUtc < ExpiresAtUtc;
}

public sealed record ManagementCredentialProofResult(
    bool IsValid, int CredentialVersion, ManagementProofFailure Failure,
    DateTimeOffset VerifiedAtUtc)
{
    public bool IsComplete => IsValid && CredentialVersion > 0 &&
        Failure == ManagementProofFailure.None && VerifiedAtUtc.Offset == TimeSpan.Zero;
}

public sealed record VerifiedBackupEvidenceReceipt(
    string ReceiptId, string BackupIdentityFingerprint, bool IsValid,
    DateTimeOffset VerifiedAtUtc, string EvidenceReference)
{
    public bool IsComplete => !string.IsNullOrWhiteSpace(ReceiptId) &&
        !string.IsNullOrWhiteSpace(BackupIdentityFingerprint) && IsValid &&
        VerifiedAtUtc.Offset == TimeSpan.Zero && !string.IsNullOrWhiteSpace(EvidenceReference);
}

public sealed record ProductionReconciliationEvidence(
    ReconciliationStatus Reconciliation, DivergenceStatus Divergence,
    string EvidenceReference, bool IsValid, bool HasUnreconciledTargetWrites = false)
{
    public bool IsComplete => IsValid && Reconciliation == ReconciliationStatus.Matched &&
        Divergence == DivergenceStatus.Synchronized && !HasUnreconciledTargetWrites && !string.IsNullOrWhiteSpace(EvidenceReference);
}

public sealed record ProductionAuditReadiness(bool IsAvailable, bool IntegrityVerified,
    string EvidenceReference)
{
    public bool IsComplete => IsAvailable && IntegrityVerified && !string.IsNullOrWhiteSpace(EvidenceReference);
}

/// <summary>
/// All execution inputs are bound to the operation, installation, software,
/// authority generation, and evidence that they were evaluated against.
/// </summary>
public sealed record ProductionExecutionContext(
    ProductionExecutionAction Action,
    ProductionExecutionScope Scope,
    string CorrelationId,
    string ApplicationVersion,
    int? SchemaVersion,
    AuthorityState SourceAuthorityState,
    AuthorityState ExpectedTargetAuthorityState,
    ProductionAuthorityGeneration AuthorityGeneration,
    ProductionPrerequisiteEvaluationReceipt PrerequisiteReceipt,
    GovernanceAuthorizationReceipt GovernanceAuthorization,
    ManagementCredentialProofResult ManagementCredentialProof,
    VerifiedBackupEvidenceReceipt BackupReceipt,
    ProductionReconciliationEvidence Reconciliation,
    ProductionAuditReadiness AuditReadiness,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    ProductionAuthorizationContract Authorization)
{
    public bool IsComplete(DateTimeOffset nowUtc)
    {
        bool expiryValid = ExpiresAtUtc is null || (ExpiresAtUtc.Value.Offset == TimeSpan.Zero &&
            IssuedAtUtc < ExpiresAtUtc.Value && nowUtc < ExpiresAtUtc.Value);
        return Scope is not null && Scope.IsComplete && !string.IsNullOrWhiteSpace(CorrelationId) &&
            !string.IsNullOrWhiteSpace(ApplicationVersion) && !ApplicationVersion.Any(char.IsWhiteSpace) &&
            (!SchemaVersion.HasValue || SchemaVersion.Value > 0) && AuthorityGeneration.IsValid &&
            IssuedAtUtc.Offset == TimeSpan.Zero && IssuedAtUtc <= nowUtc && expiryValid &&
            PrerequisiteReceipt?.IsComplete == true && GovernanceAuthorization?.IsComplete(nowUtc) == true &&
            ManagementCredentialProof?.IsComplete == true && BackupReceipt?.IsComplete == true &&
            Reconciliation?.IsComplete == true && AuditReadiness?.IsComplete == true;
    }
}

public sealed record ProductionAuthorizationContract(
    ProductionExecutionAction Action,
    ProductionExecutionScope Scope,
    string ApplicationVersion,
    int? SchemaVersion,
    string CorrelationId,
    AuthorityState SourceAuthorityState,
    AuthorityState TargetAuthorityState,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string DecisionReference,
    string ProjectOwnerGovernanceReference);

public sealed record ProductionAuthorizationValidationResult(bool IsValid, IReadOnlyList<string> Issues)
{
    public static ProductionAuthorizationValidationResult Invalid(params string[] issues) => new(false, issues);
}

public static class ProductionAuthorizationContractValidator
{
    public static ProductionAuthorizationValidationResult Validate(
        ProductionAuthorizationContract? contract, ProductionExecutionContext? context, DateTimeOffset nowUtc)
    {
        var issues = new List<string>();
        if (contract is null) issues.Add("authorization-contract-required");
        if (context is null) issues.Add("execution-context-required");
        if (contract is null || context is null) return new(false, new ReadOnlyCollection<string>(issues));
        if (!context.IsComplete(nowUtc)) issues.Add("execution-context-incomplete");
        if (contract.Action != context.Action) issues.Add("authorization-action-mismatch");
        if (contract.Scope != context.Scope) issues.Add("authorization-scope-mismatch");
        if (!StringComparer.Ordinal.Equals(contract.ApplicationVersion, context.ApplicationVersion)) issues.Add("authorization-version-mismatch");
        if (contract.SchemaVersion != context.SchemaVersion) issues.Add("authorization-schema-mismatch");
        if (!StringComparer.Ordinal.Equals(contract.CorrelationId, context.CorrelationId)) issues.Add("authorization-correlation-mismatch");
        if (contract.SourceAuthorityState != context.SourceAuthorityState) issues.Add("authorization-source-state-mismatch");
        if (contract.TargetAuthorityState != context.ExpectedTargetAuthorityState) issues.Add("authorization-target-state-mismatch");
        if (contract.IssuedAtUtc.Offset != TimeSpan.Zero || contract.ExpiresAtUtc.Offset != TimeSpan.Zero ||
            contract.IssuedAtUtc > nowUtc || contract.ExpiresAtUtc <= nowUtc || contract.ExpiresAtUtc <= contract.IssuedAtUtc)
            issues.Add("authorization-time-invalid");
        if (string.IsNullOrWhiteSpace(contract.DecisionReference)) issues.Add("authorization-decision-reference-required");
        if (string.IsNullOrWhiteSpace(contract.ProjectOwnerGovernanceReference)) issues.Add("authorization-owner-reference-required");
        return issues.Count == 0 ? new(true, Array.Empty<string>()) :
            new(false, new ReadOnlyCollection<string>(issues));
    }
}

public interface IProductionAuthorizationVerifier
{
    Task<bool> VerifyAsync(ProductionAuthorizationContract contract, ProductionExecutionContext context,
        DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
}

/// <summary>Installed in any production composition unless a future governed deployment explicitly replaces it.</summary>
public sealed class ProductionAuthorizationDisabledVerifier : IProductionAuthorizationVerifier
{
    public Task<bool> VerifyAsync(ProductionAuthorizationContract contract, ProductionExecutionContext context,
        DateTimeOffset nowUtc, CancellationToken cancellationToken = default) => Task.FromResult(false);
}

/// <summary>Qualification-only verifier. It validates contract shape but is not a production authority.</summary>
public sealed class QualificationProductionAuthorizationVerifier : IProductionAuthorizationVerifier
{
    public Task<bool> VerifyAsync(ProductionAuthorizationContract contract, ProductionExecutionContext context,
        DateTimeOffset nowUtc, CancellationToken cancellationToken = default) =>
        Task.FromResult(ProductionAuthorizationContractValidator.Validate(contract, context, nowUtc).IsValid &&
            context.GovernanceAuthorization.ExplicitlyAuthorized);
}

public sealed record WriterFenceRequest(string ScopeKey, long Generation, string CorrelationId)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(ScopeKey) && Generation >= 0 && !string.IsNullOrWhiteSpace(CorrelationId);
}

public enum WriterFenceAcquireStatus { Acquired, Rejected, RecoveryRequired }
public sealed record WriterFenceAcquireResult(WriterFenceAcquireStatus Status, string Reason, WriterFenceLease? Lease)
{
    public bool Acquired => Status == WriterFenceAcquireStatus.Acquired && Lease is not null;
}

public interface IProductionWriterFence
{
    Task<WriterFenceAcquireResult> AcquireAsync(WriterFenceRequest request, CancellationToken cancellationToken = default);
}

public sealed class WriterFenceLease : IAsyncDisposable
{
    private readonly Action _release;
    private int _released;
    internal WriterFenceLease(long generation, bool recovered, Action release)
    {
        Generation = generation;
        WasRecoveredFromAbandonment = recovered;
        _release = release;
    }
    public long Generation { get; }
    public bool WasRecoveredFromAbandonment { get; }
    public bool IsReleased => Volatile.Read(ref _released) != 0;
    public void EnsureCurrent(long currentGeneration)
    {
        if (IsReleased || currentGeneration != Generation)
            throw new InvalidOperationException("stale-writer-fence");
    }
    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _released, 1) == 0) _release();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Local crash-safe fencing: the named mutex prevents a second process and the
/// exclusive lock file protects against processes that do not share the same
/// managed object. OS ownership releases both on process termination.
/// </summary>
public sealed class LocalSingleWriterFence : IProductionWriterFence
{
    private readonly string _lockPath;
    private readonly string _mutexName;
    private readonly JsonSerializerOptions _json = new() { WriteIndented = false };

    public LocalSingleWriterFence(string lockPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockPath);
        _lockPath = Path.GetFullPath(lockPath);
        Directory.CreateDirectory(Path.GetDirectoryName(_lockPath)!);
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_lockPath))).ToLowerInvariant();
        _mutexName = "Local\\RahNegar-ProductionWriter-" + key;
    }

    public Task<WriterFenceAcquireResult> AcquireAsync(WriterFenceRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request is null || !request.IsValid) return Task.FromResult(new WriterFenceAcquireResult(WriterFenceAcquireStatus.Rejected, "writer-fence-request-invalid", null));
        var mutex = new Mutex(false, _mutexName);
        bool held = false;
        bool recovered = false;
        FileStream? stream = null;
        try
        {
            try { held = mutex.WaitOne(0); }
            catch (AbandonedMutexException) { held = true; recovered = true; }
            if (!held) return Task.FromResult(new WriterFenceAcquireResult(WriterFenceAcquireStatus.Rejected, "writer-fence-already-held", null));
            try { stream = new FileStream(_lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.WriteThrough); }
            catch
            {
                mutex.ReleaseMutex();
                return Task.FromResult(new WriterFenceAcquireResult(WriterFenceAcquireStatus.Rejected, "writer-fence-file-already-held", null));
            }
            if (stream.Length > 0)
            {
                stream.Position = 0;
                using var reader = new StreamReader(stream, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true, leaveOpen: true);
                string existing = reader.ReadToEnd();
                FenceEnvelope? envelope = JsonSerializer.Deserialize<FenceEnvelope>(existing, _json);
                if (envelope is null || string.IsNullOrWhiteSpace(envelope.Payload) ||
                    !StringComparer.OrdinalIgnoreCase.Equals(envelope.IntegritySha256,
                        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(envelope.Payload)))))
                    throw new InvalidDataException("writer-fence-metadata-integrity-failed");
                stream.Position = 0;
            }
            stream.SetLength(0);
            var metadata = new FenceMetadata(request.ScopeKey, request.Generation, request.CorrelationId, DateTimeOffset.UtcNow);
            string payload = JsonSerializer.Serialize(metadata, _json);
            string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true))
            {
                writer.Write(JsonSerializer.Serialize(new FenceEnvelope(payload, digest), _json));
                writer.Flush();
            }
            stream.Flush(true);
            return Task.FromResult(new WriterFenceAcquireResult(WriterFenceAcquireStatus.Acquired, recovered ? "writer-fence-acquired-after-abandonment" : "writer-fence-acquired",
                new WriterFenceLease(request.Generation, recovered, () => Release(stream, mutex))));
        }
        catch
        {
            if (held) { try { mutex.ReleaseMutex(); } catch { } }
            mutex.Dispose();
            return Task.FromResult(new WriterFenceAcquireResult(WriterFenceAcquireStatus.RecoveryRequired, "writer-fence-corrupt-or-unavailable", null));
        }
    }

    private void Release(FileStream stream, Mutex mutex)
    {
            stream?.Dispose();
        try { mutex.ReleaseMutex(); } catch { }
        mutex.Dispose();
    }

    private sealed record FenceMetadata(string ScopeKey, long Generation, string CorrelationId, DateTimeOffset AcquiredAtUtc);
    private sealed record FenceEnvelope(string Payload, string IntegritySha256);
}

public interface IProductionWriteDrain
{
    Task<LegacyWriteLeaseResult> EnterLegacyWriteAsync(long generation, CancellationToken cancellationToken = default);
    Task<WriteDrainLeaseResult> AcquireDrainAsync(CancellationToken cancellationToken = default);
    void AbortDrain();
    void CommitTarget(long generation);
    void EnableTargetRouting(long generation);
    void CommitLegacy(long generation);
    Task<TargetWriteLeaseResult> EnterTargetWriteAsync(long generation, CancellationToken cancellationToken = default);
}

public enum WriteGateStatus { Acquired, Rejected }
public sealed record LegacyWriteLeaseResult(WriteGateStatus Status, string Reason, LegacyWriteLease? Lease)
{ public bool Acquired => Status == WriteGateStatus.Acquired && Lease is not null; }
public sealed record TargetWriteLeaseResult(WriteGateStatus Status, string Reason, TargetWriteLease? Lease)
{ public bool Acquired => Status == WriteGateStatus.Acquired && Lease is not null; }
public sealed record WriteDrainLeaseResult(WriteGateStatus Status, string Reason, WriteDrainLease? Lease)
{ public bool Acquired => Status == WriteGateStatus.Acquired && Lease is not null; }

public sealed class LegacyWriteLease : IAsyncDisposable
{
    private readonly Action _release;
    private readonly Func<long, bool> _isCurrent;
    private int _done;
    internal LegacyWriteLease(long generation, Action release, Func<long, bool> isCurrent) { Generation = generation; _release = release; _isCurrent = isCurrent; }
    public long Generation { get; }
    public void EnsureCurrent(long generation) { if (Volatile.Read(ref _done) != 0 || generation != Generation || !_isCurrent(generation)) throw new InvalidOperationException("stale-legacy-write-lease"); }
    public ValueTask DisposeAsync() { if (Interlocked.Exchange(ref _done, 1) == 0) _release(); return ValueTask.CompletedTask; }
}
public sealed class TargetWriteLease : IAsyncDisposable
{
    private readonly Action _release;
    private readonly Func<long, bool> _isCurrent;
    private int _done;
    internal TargetWriteLease(long generation, Action release, Func<long, bool> isCurrent) { Generation = generation; _release = release; _isCurrent = isCurrent; }
    public long Generation { get; }
    public void EnsureCurrent(long generation) { if (Volatile.Read(ref _done) != 0 || generation != Generation || !_isCurrent(generation)) throw new InvalidOperationException("stale-target-write-lease"); }
    public ValueTask DisposeAsync() { if (Interlocked.Exchange(ref _done, 1) == 0) _release(); return ValueTask.CompletedTask; }
}
public sealed class WriteDrainLease : IAsyncDisposable
{
    private readonly ProductionWriteGate _owner;
    private int _done;
    internal WriteDrainLease(ProductionWriteGate owner) { _owner = owner; }
    public void MarkCommitted() => _owner.MarkDrainCommitted(this);
    public ValueTask DisposeAsync() { if (Interlocked.Exchange(ref _done, 1) == 0) _owner.ReleaseDrain(this); return ValueTask.CompletedTask; }
}

/// <summary>Service-level barrier used by every future write adapter.</summary>
public sealed class ProductionWriteGate : IProductionWriteDrain
{
    private readonly object _sync = new();
    private TaskCompletionSource<bool>? _drainCompletion;
    private int _activeWrites;
    private bool _draining;
    private bool _targetAuthoritative;
    private bool _targetRouting;
    private long _generation;

    public Task<LegacyWriteLeaseResult> EnterLegacyWriteAsync(long generation, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (_draining || _targetAuthoritative || generation != _generation)
                return Task.FromResult(new LegacyWriteLeaseResult(WriteGateStatus.Rejected, "legacy-write-fenced", null));
            _activeWrites++;
            return Task.FromResult(new LegacyWriteLeaseResult(WriteGateStatus.Acquired, "legacy-write-admitted", new LegacyWriteLease(generation, ReleaseWrite, IsLegacyLeaseCurrent)));
        }
    }

    public async Task<WriteDrainLeaseResult> AcquireDrainAsync(CancellationToken cancellationToken = default)
    {
        Task wait;
        lock (_sync)
        {
            if (_draining) return new(WriteGateStatus.Rejected, "write-drain-already-held", null);
            _draining = true;
            if (_activeWrites == 0) return new(WriteGateStatus.Acquired, "write-drain-acquired", new WriteDrainLease(this));
            _drainCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            wait = _drainCompletion.Task;
        }
        try
        {
            await wait.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new(WriteGateStatus.Acquired, "write-drain-acquired", new WriteDrainLease(this));
        }
        catch
        {
            lock (_sync)
            {
                if (ReferenceEquals(wait, _drainCompletion?.Task))
                {
                    _draining = false;
                    _drainCompletion = null;
                }
            }
            throw;
        }
    }

    public void AbortDrain() { lock (_sync) { if (!_targetAuthoritative) _draining = false; } }
    public void CommitTarget(long generation) { lock (_sync) { if (!_draining) throw new InvalidOperationException("write-drain-required"); _generation = generation; _targetAuthoritative = true; _targetRouting = false; } }
    public void EnableTargetRouting(long generation) { lock (_sync) { if (!_targetAuthoritative || generation != _generation) throw new InvalidOperationException("target-authority-required"); _targetRouting = true; } }
    public void CommitLegacy(long generation) { lock (_sync) { _generation = generation; _targetAuthoritative = false; _targetRouting = false; _draining = false; } }
    public Task<TargetWriteLeaseResult> EnterTargetWriteAsync(long generation, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (!_targetAuthoritative || !_targetRouting || generation != _generation)
                return Task.FromResult(new TargetWriteLeaseResult(WriteGateStatus.Rejected, "target-write-before-authority-or-routing", null));
            _activeWrites++;
            return Task.FromResult(new TargetWriteLeaseResult(WriteGateStatus.Acquired, "target-write-admitted", new TargetWriteLease(generation, ReleaseWrite, IsTargetLeaseCurrent)));
        }
    }
    internal void MarkDrainCommitted(WriteDrainLease lease) { lock (_sync) { _draining = false; } }
    internal void ReleaseDrain(WriteDrainLease lease) { lock (_sync) { _draining = false; } }
    private void ReleaseWrite() { lock (_sync) { _activeWrites--; if (_activeWrites == 0) _drainCompletion?.TrySetResult(true); } }
    private bool IsLegacyLeaseCurrent(long generation) { lock (_sync) return !_targetAuthoritative && !_draining && _generation == generation; }
    private bool IsTargetLeaseCurrent(long generation) { lock (_sync) return _targetAuthoritative && _targetRouting && _generation == generation; }
}

public sealed record ProductionAuthoritySnapshot(AuthorityStateRecord Record);
public interface IProductionAuthorityStore
{
    Task<ProductionAuthoritySnapshot> ReadAsync(CancellationToken cancellationToken = default);
    Task<bool> CommitTargetAsync(ProductionAuthoritySnapshot expected, ProductionExecutionContext context, long newGeneration, CancellationToken cancellationToken = default);
    Task<bool> CommitLegacyAsync(ProductionAuthoritySnapshot expected, ProductionExecutionContext context, long newGeneration, CancellationToken cancellationToken = default);
    Task<bool> EnableTargetRoutingAsync(long generation, CancellationToken cancellationToken = default);
    Task<bool> DisableTargetRoutingAsync(CancellationToken cancellationToken = default);
    Task EnterRecoveryAsync(string correlationId, string reason, long generation, CancellationToken cancellationToken = default);
}

/// <summary>Atomic authority adapter for qualification and future composition.</summary>
public sealed class InMemoryProductionAuthorityStore : IProductionAuthorityStore
{
    private readonly object _sync = new();
    private AuthorityStateRecord _record;
    public bool FailTargetCommit { get; set; }
    public bool FailTargetRouting { get; set; }
    public bool FailLegacyCommit { get; set; }
    public InMemoryProductionAuthorityStore(AuthorityStateRecord? initial = null) => _record = initial ?? AuthorityStateRecord.Legacy("qualification", "all");
    public Task<ProductionAuthoritySnapshot> ReadAsync(CancellationToken cancellationToken = default) { lock (_sync) return Task.FromResult(new ProductionAuthoritySnapshot(_record)); }
    public Task<bool> CommitTargetAsync(ProductionAuthoritySnapshot expected, ProductionExecutionContext context, long newGeneration, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (FailTargetCommit) return Task.FromResult(false);
            if (!Same(expected.Record) || _record.State != AuthorityState.LegacyAuthoritative || newGeneration <= _record.AuthorityEpoch) return Task.FromResult(false);
            _record = _record with { State = AuthorityState.TargetAuthoritative, LegacyAuthoritative = false, TargetAuthoritative = true, TargetRoutingEnabled = false,
                CorrelationId = context.CorrelationId, Reason = "phase9.7-authority-commit", AuthorityEpoch = newGeneration, Revision = _record.Revision + 1, RecordedAtUtc = DateTimeOffset.UtcNow };
            return Task.FromResult(true);
        }
    }
    public Task<bool> CommitLegacyAsync(ProductionAuthoritySnapshot expected, ProductionExecutionContext context, long newGeneration, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (FailLegacyCommit) return Task.FromResult(false);
            if (!Same(expected.Record) || _record.State != AuthorityState.TargetAuthoritative || newGeneration <= _record.AuthorityEpoch) return Task.FromResult(false);
            _record = _record with { State = AuthorityState.LegacyAuthoritative, LegacyAuthoritative = true, TargetAuthoritative = false, TargetRoutingEnabled = false,
                CorrelationId = context.CorrelationId, Reason = "phase9.7-rollback-commit", AuthorityEpoch = newGeneration, Revision = _record.Revision + 1, RecordedAtUtc = DateTimeOffset.UtcNow };
            return Task.FromResult(true);
        }
    }
    public Task<bool> EnableTargetRoutingAsync(long generation, CancellationToken cancellationToken = default)
    { lock (_sync) { if (FailTargetRouting || _record.State != AuthorityState.TargetAuthoritative || _record.AuthorityEpoch != generation) return Task.FromResult(false); _record = _record with { TargetRoutingEnabled = true, Revision = _record.Revision + 1, RecordedAtUtc = DateTimeOffset.UtcNow }; return Task.FromResult(true); } }
    public Task<bool> DisableTargetRoutingAsync(CancellationToken cancellationToken = default)
    { lock (_sync) { if (!_record.TargetRoutingEnabled) return Task.FromResult(true); _record = _record with { TargetRoutingEnabled = false, Revision = _record.Revision + 1, RecordedAtUtc = DateTimeOffset.UtcNow }; return Task.FromResult(true); } }
    public Task EnterRecoveryAsync(string correlationId, string reason, long generation, CancellationToken cancellationToken = default)
    { lock (_sync) { _record = _record with { State = AuthorityState.RecoveryRequired, LegacyAuthoritative = false, TargetAuthoritative = false, TargetRoutingEnabled = false, CorrelationId = correlationId, Reason = reason, AuthorityEpoch = Math.Max(_record.AuthorityEpoch, generation), Revision = _record.Revision + 1, RecordedAtUtc = DateTimeOffset.UtcNow }; return Task.CompletedTask; } }
    private bool Same(AuthorityStateRecord other) => _record.Revision == other.Revision && _record.AuthorityEpoch == other.AuthorityEpoch && _record.State == other.State;
}

/// <summary>Durable adapter for the existing integrity-checked authority file.</summary>
public sealed class FileProductionAuthorityStore : IProductionAuthorityStore
{
    private readonly IAuthorityStateStore _store;
    private readonly SemaphoreSlim _gate = new(1, 1);
    public FileProductionAuthorityStore(IAuthorityStateStore store) => _store = store ?? throw new ArgumentNullException(nameof(store));
    public async Task<ProductionAuthoritySnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        AuthorityLoadResult loaded = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!loaded.IsOperationallySafe) throw new InvalidDataException("authority-state-not-operational");
        return new(loaded.Record);
    }
    public async Task<bool> CommitTargetAsync(ProductionAuthoritySnapshot expected, ProductionExecutionContext context, long newGeneration, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ProductionAuthoritySnapshot current = await ReadAsync(cancellationToken).ConfigureAwait(false);
            if (!Matches(current.Record, expected.Record) || current.Record.State != AuthorityState.LegacyAuthoritative || newGeneration <= current.Record.AuthorityEpoch) return false;
            await _store.SaveAsync(current.Record with { State = AuthorityState.TargetAuthoritative, LegacyAuthoritative = false, TargetAuthoritative = true, TargetRoutingEnabled = false, CorrelationId = context.CorrelationId, Reason = "phase9.7-authority-commit", AuthorityEpoch = newGeneration, Revision = current.Record.Revision + 1, RecordedAtUtc = DateTimeOffset.UtcNow }, cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally { _gate.Release(); }
    }
    public async Task<bool> CommitLegacyAsync(ProductionAuthoritySnapshot expected, ProductionExecutionContext context, long newGeneration, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ProductionAuthoritySnapshot current = await ReadAsync(cancellationToken).ConfigureAwait(false);
            if (!Matches(current.Record, expected.Record) || current.Record.State != AuthorityState.TargetAuthoritative || newGeneration <= current.Record.AuthorityEpoch) return false;
            await _store.SaveAsync(current.Record with { State = AuthorityState.LegacyAuthoritative, LegacyAuthoritative = true, TargetAuthoritative = false, TargetRoutingEnabled = false, CorrelationId = context.CorrelationId, Reason = "phase9.7-rollback-commit", AuthorityEpoch = newGeneration, Revision = current.Record.Revision + 1, RecordedAtUtc = DateTimeOffset.UtcNow }, cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally { _gate.Release(); }
    }
    public async Task<bool> EnableTargetRoutingAsync(long generation, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { ProductionAuthoritySnapshot current = await ReadAsync(cancellationToken).ConfigureAwait(false); if (current.Record.State != AuthorityState.TargetAuthoritative || current.Record.AuthorityEpoch != generation) return false; await _store.SaveAsync(current.Record with { TargetRoutingEnabled = true, Revision = current.Record.Revision + 1, RecordedAtUtc = DateTimeOffset.UtcNow }, cancellationToken).ConfigureAwait(false); return true; }
        finally { _gate.Release(); }
    }
    public async Task<bool> DisableTargetRoutingAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { ProductionAuthoritySnapshot current = await ReadAsync(cancellationToken).ConfigureAwait(false); if (!current.Record.TargetRoutingEnabled) return true; await _store.SaveAsync(current.Record with { TargetRoutingEnabled = false, Revision = current.Record.Revision + 1, RecordedAtUtc = DateTimeOffset.UtcNow }, cancellationToken).ConfigureAwait(false); return true; }
        finally { _gate.Release(); }
    }
    public async Task EnterRecoveryAsync(string correlationId, string reason, long generation, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { AuthorityLoadResult current = await _store.LoadAsync(CancellationToken.None).ConfigureAwait(false); if (!current.IsOperationallySafe) return; await _store.SaveAsync(current.Record with { State = AuthorityState.RecoveryRequired, LegacyAuthoritative = false, TargetAuthoritative = false, TargetRoutingEnabled = false, CorrelationId = correlationId, Reason = reason, AuthorityEpoch = Math.Max(current.Record.AuthorityEpoch, generation), Revision = current.Record.Revision + 1, RecordedAtUtc = DateTimeOffset.UtcNow }, CancellationToken.None).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }
    private static bool Matches(AuthorityStateRecord left, AuthorityStateRecord right) => left.Revision == right.Revision && left.AuthorityEpoch == right.AuthorityEpoch && left.State == right.State;
}

public enum ProductionExecutionStatus { Succeeded, Rejected, RecoveryRequired }
public sealed record ProductionExecutionResult(ProductionExecutionStatus Status, string Reason,
    IReadOnlyList<string> CompletedStages, bool AuthorityChanged, bool RoutingChanged)
{
    public bool Succeeded => Status == ProductionExecutionStatus.Succeeded;
}

public sealed class GovernedProductionActivationExecutor
{
    private readonly IClock _clock;
    private readonly IProductionAuthorizationVerifier _authorization;
    private readonly IProductionWriterFence _fence;
    private readonly IProductionWriteDrain _writes;
    private readonly IProductionAuthorityStore _authority;
    private readonly IAuthorityAuditSink _audit;

    public GovernedProductionActivationExecutor(IClock clock, IProductionWriterFence fence,
        IProductionWriteDrain writes, IProductionAuthorityStore authority, IAuthorityAuditSink audit)
        : this(clock, new ProductionAuthorizationDisabledVerifier(), fence, writes, authority, audit) { }

    public GovernedProductionActivationExecutor(IClock clock, IProductionAuthorizationVerifier authorization,
        IProductionWriterFence fence, IProductionWriteDrain writes, IProductionAuthorityStore authority, IAuthorityAuditSink audit) =>
        (_clock, _authorization, _fence, _writes, _authority, _audit) = (clock ?? throw new ArgumentNullException(nameof(clock)), authorization ?? throw new ArgumentNullException(nameof(authorization)), fence ?? throw new ArgumentNullException(nameof(fence)), writes ?? throw new ArgumentNullException(nameof(writes)), authority ?? throw new ArgumentNullException(nameof(authority)), audit ?? throw new ArgumentNullException(nameof(audit)));

    public async Task<ProductionExecutionResult> ExecuteAsync(ProductionExecutionContext? context, CancellationToken cancellationToken = default)
    {
        var stages = new List<string>();
        DateTimeOffset now = _clock.UtcNow.ToUniversalTime();
        ProductionExecutionResult Reject(string reason) => new(ProductionExecutionStatus.Rejected, reason, stages.AsReadOnly(), false, false);
        if (context is null || context.Action != ProductionExecutionAction.Activate) return Reject("activation-context-required");
        if (!context.IsComplete(now)) return Reject("activation-context-incomplete");
        if (!ProductionAuthorizationContractValidator.Validate(context.Authorization, context, now).IsValid) return Reject("authorization-contract-invalid");
        stages.Add("prerequisite-validation");
        if (!await _authorization.VerifyAsync(context.Authorization, context, now, cancellationToken).ConfigureAwait(false)) return Reject("governance-authorization-not-present-or-not-verifiable");
        stages.Add("authorization-validation");
        if (context.SourceAuthorityState != AuthorityState.LegacyAuthoritative || context.ExpectedTargetAuthorityState != AuthorityState.TargetAuthoritative) return Reject("authority-state-contract-invalid");
        WriterFenceAcquireResult acquired = await _fence.AcquireAsync(new WriterFenceRequest(context.Scope.DeploymentScope, context.AuthorityGeneration.Generation + 1, context.CorrelationId), cancellationToken).ConfigureAwait(false);
        if (!acquired.Acquired) return Reject(acquired.Reason);
        await using WriterFenceLease lease = acquired.Lease!;
        WriteDrainLease? drain = null;
        bool authorityChanged = false;
        try
        {
            stages.Add("writer-fence-acquired");
            ProductionAuthoritySnapshot current = await _authority.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (current.Record.State != AuthorityState.LegacyAuthoritative || current.Record.TargetAuthoritative || current.Record.TargetRoutingEnabled || current.Record.AuthorityEpoch != context.AuthorityGeneration.Epoch ||
                !StringComparer.Ordinal.Equals(current.Record.DeploymentScope, context.Scope.DeploymentScope) ||
                !StringComparer.Ordinal.Equals(current.Record.StationScope, context.Scope.StationScope))
                return Reject("current-authority-state-mismatch");
            WriteDrainLeaseResult drained = await _writes.AcquireDrainAsync(cancellationToken).ConfigureAwait(false);
            if (!drained.Acquired) return Reject(drained.Reason);
            drain = drained.Lease;
            stages.Add("legacy-writes-drained");
            lease.EnsureCurrent(context.AuthorityGeneration.Generation + 1);
            if (!context.Reconciliation.IsComplete) return Reject("final-reconciliation-or-divergence-not-safe");
            if (!context.BackupReceipt.IsComplete) return Reject("verified-backup-evidence-invalid");
            stages.Add("final-reconciliation-and-backup-verified");
            await WriteAuditAsync(context, AuthorityAuditAction.CommitAttempt, "PREPARED", "durable-audit-prepare", context.AuthorityGeneration.Epoch, cancellationToken).ConfigureAwait(false);
            stages.Add("durable-audit-prepared");
            if (!await _authority.CommitTargetAsync(current, context, context.AuthorityGeneration.Epoch + 1, cancellationToken).ConfigureAwait(false))
                return await RecoveryAsync(context, drain, "authority-commit-rejected", stages, authorityChanged, cancellationToken).ConfigureAwait(false);
            authorityChanged = true;
            _writes.CommitTarget(context.AuthorityGeneration.Epoch + 1);
            stages.Add("canonical-authority-committed");
            ProductionAuthoritySnapshot committed = await _authority.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (committed.Record.State != AuthorityState.TargetAuthoritative || !committed.Record.TargetAuthoritative || committed.Record.LegacyAuthoritative || committed.Record.TargetRoutingEnabled)
                return await RecoveryAsync(context, drain, "post-commit-authority-verification-failed", stages, authorityChanged, cancellationToken).ConfigureAwait(false);
            if (!await _authority.EnableTargetRoutingAsync(context.AuthorityGeneration.Epoch + 1, cancellationToken).ConfigureAwait(false))
                return await RecoveryAsync(context, drain, "target-routing-enable-failed", stages, authorityChanged, cancellationToken).ConfigureAwait(false);
            _writes.EnableTargetRouting(context.AuthorityGeneration.Epoch + 1);
            stages.Add("target-routing-enabled-after-authority");
            ProductionAuthoritySnapshot routed = await _authority.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (!AuthorityRoutingGuard.IsTargetOperationalRoutingAllowed(routed.Record) || routed.Record.LegacyAuthoritative)
                return await RecoveryAsync(context, drain, "routing-postcondition-failed", stages, authorityChanged, cancellationToken).ConfigureAwait(false);
            await WriteAuditAsync(context, AuthorityAuditAction.CommitSucceeded, "SUCCEEDED", "durable-audit-finalize", context.AuthorityGeneration.Epoch + 1, cancellationToken).ConfigureAwait(false);
            drain!.MarkCommitted();
            stages.Add("durable-audit-finalized");
            return new(ProductionExecutionStatus.Succeeded, "activation-complete", stages.AsReadOnly(), authorityChanged, true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested && !authorityChanged) { _writes.AbortDrain(); throw; }
        catch (Exception ex)
        {
            return await RecoveryAsync(context, drain, "activation-failure-" + ex.GetType().Name, stages, authorityChanged, cancellationToken).ConfigureAwait(false);
        }
        finally { if (!authorityChanged) _writes.AbortDrain(); if (drain is not null) await drain.DisposeAsync().ConfigureAwait(false); }
    }

    private async Task<ProductionExecutionResult> RecoveryAsync(ProductionExecutionContext context, WriteDrainLease? drain, string reason, List<string> stages, bool authorityChanged, CancellationToken token)
    {
        try { await _authority.DisableTargetRoutingAsync(CancellationToken.None).ConfigureAwait(false); await _authority.EnterRecoveryAsync(context.CorrelationId, reason, context.AuthorityGeneration.Epoch + 1, CancellationToken.None).ConfigureAwait(false); }
        catch { }
        _writes.AbortDrain();
        stages.Add("recovery-required");
        try { await WriteAuditAsync(context, AuthorityAuditAction.RecoveryRequiredEntered, "RECOVERY_REQUIRED", reason, context.AuthorityGeneration.Epoch + 1, CancellationToken.None).ConfigureAwait(false); } catch { }
        return new(ProductionExecutionStatus.RecoveryRequired, reason, stages.AsReadOnly(), authorityChanged, false);
    }

    private Task WriteAuditAsync(ProductionExecutionContext context, AuthorityAuditAction action, string result, string reason, long generation, CancellationToken token) =>
        _audit.WriteAsync(new(context.CorrelationId, _clock.UtcNow.ToUniversalTime(), context.Scope.DeploymentScope, context.Scope.StationScope,
            context.ApplicationVersion, context.SourceAuthorityState, context.ExpectedTargetAuthorityState, action, result, reason,
            context.PrerequisiteReceipt.EvidenceReference, generation, context.Scope.ProfileScope,
            context.ManagementCredentialProof.IsComplete ? "VALID" : "INVALID"), token);
}

public enum ProductionRollbackEligibilityStatus { ROLLBACK_ELIGIBLE, ROLLBACK_NOT_SAFE, RECOVERY_REQUIRED }
public sealed record ProductionRollbackEligibility(
    ProductionRollbackEligibilityStatus Status, IReadOnlyList<string> Reasons)
{
    public bool IsEligible => Status == ProductionRollbackEligibilityStatus.ROLLBACK_ELIGIBLE;
    public static ProductionRollbackEligibility Evaluate(ProductionExecutionContext context, DateTimeOffset nowUtc)
    {
        if (context is null || context.Action != ProductionExecutionAction.Rollback)
            return new(ProductionRollbackEligibilityStatus.RECOVERY_REQUIRED, ["rollback-context-invalid"]);
        if (context.SourceAuthorityState != AuthorityState.TargetAuthoritative || context.ExpectedTargetAuthorityState != AuthorityState.LegacyAuthoritative)
            return new(ProductionRollbackEligibilityStatus.ROLLBACK_NOT_SAFE, ["rollback-authority-state-mismatch"]);
        if (context.Reconciliation is null || context.Reconciliation.HasUnreconciledTargetWrites ||
            context.Reconciliation.Reconciliation != ReconciliationStatus.Matched || context.Reconciliation.Divergence != DivergenceStatus.Synchronized)
            return new(ProductionRollbackEligibilityStatus.ROLLBACK_NOT_SAFE, ["rollback-reconciliation-or-divergence-not-safe"]);
        if (context.BackupReceipt is null || !context.BackupReceipt.IsComplete) return new(ProductionRollbackEligibilityStatus.ROLLBACK_NOT_SAFE, ["rollback-backup-invalid"]);
        if (context.AuditReadiness is null || !context.AuditReadiness.IsComplete) return new(ProductionRollbackEligibilityStatus.ROLLBACK_NOT_SAFE, ["rollback-audit-unavailable"]);
        if (!context.IsComplete(nowUtc)) return new(ProductionRollbackEligibilityStatus.RECOVERY_REQUIRED, ["rollback-context-invalid"]);
        return new(ProductionRollbackEligibilityStatus.ROLLBACK_ELIGIBLE, ["rollback-evidence-complete"]);
    }
}

public sealed class GovernedProductionRollbackExecutor
{
    private readonly IClock _clock;
    private readonly IProductionAuthorizationVerifier _authorization;
    private readonly IProductionWriterFence _fence;
    private readonly IProductionWriteDrain _writes;
    private readonly IProductionAuthorityStore _authority;
    private readonly IAuthorityAuditSink _audit;
    public GovernedProductionRollbackExecutor(IClock clock, IProductionWriterFence fence,
        IProductionWriteDrain writes, IProductionAuthorityStore authority, IAuthorityAuditSink audit)
        : this(clock, new ProductionAuthorizationDisabledVerifier(), fence, writes, authority, audit) { }
    public GovernedProductionRollbackExecutor(IClock clock, IProductionAuthorizationVerifier authorization, IProductionWriterFence fence, IProductionWriteDrain writes, IProductionAuthorityStore authority, IAuthorityAuditSink audit) =>
        (_clock, _authorization, _fence, _writes, _authority, _audit) = (clock ?? throw new ArgumentNullException(nameof(clock)), authorization ?? throw new ArgumentNullException(nameof(authorization)), fence ?? throw new ArgumentNullException(nameof(fence)), writes ?? throw new ArgumentNullException(nameof(writes)), authority ?? throw new ArgumentNullException(nameof(authority)), audit ?? throw new ArgumentNullException(nameof(audit)));

    public async Task<ProductionExecutionResult> ExecuteAsync(ProductionExecutionContext? context, CancellationToken cancellationToken = default)
    {
        var stages = new List<string>();
        DateTimeOffset now = _clock.UtcNow.ToUniversalTime();
        ProductionExecutionResult Reject(string reason) => new(ProductionExecutionStatus.Rejected, reason, stages.AsReadOnly(), false, false);
        if (context is null || context.Action != ProductionExecutionAction.Rollback) return Reject("rollback-context-required");
        ProductionRollbackEligibility eligibility = ProductionRollbackEligibility.Evaluate(context, now);
        if (!eligibility.IsEligible) return Reject(string.Join(';', eligibility.Reasons));
        if (!ProductionAuthorizationContractValidator.Validate(context.Authorization, context, now).IsValid ||
            !await _authorization.VerifyAsync(context.Authorization, context, now, cancellationToken).ConfigureAwait(false)) return Reject("rollback-authorization-not-present-or-invalid");
        WriterFenceAcquireResult acquired = await _fence.AcquireAsync(new WriterFenceRequest(context.Scope.DeploymentScope, context.AuthorityGeneration.Epoch + 1, context.CorrelationId), cancellationToken).ConfigureAwait(false);
        if (!acquired.Acquired) return Reject(acquired.Reason);
        await using WriterFenceLease lease = acquired.Lease!;
        ProductionAuthoritySnapshot current = await _authority.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (current.Record.State != AuthorityState.TargetAuthoritative || current.Record.AuthorityEpoch != context.AuthorityGeneration.Epoch)
            return Reject("rollback-generation-or-authority-stale");
        WriteDrainLeaseResult drained = await _writes.AcquireDrainAsync(cancellationToken).ConfigureAwait(false);
        if (!drained.Acquired) return Reject(drained.Reason);
        WriteDrainLease drain = drained.Lease!;
        try
        {
            stages.Add("rollback-writer-fence-and-drain");
            await _audit.WriteAsync(new(context.CorrelationId, now, context.Scope.DeploymentScope, context.Scope.StationScope, context.ApplicationVersion,
                AuthorityState.TargetAuthoritative, AuthorityState.LegacyAuthoritative, AuthorityAuditAction.RollbackAttempt, "ATTEMPTED", "rollback-prepared", context.PrerequisiteReceipt.EvidenceReference, context.AuthorityGeneration.Epoch, context.Scope.ProfileScope, context.ManagementCredentialProof.IsComplete ? "VALID" : "INVALID"), cancellationToken).ConfigureAwait(false);
            if (!await _authority.CommitLegacyAsync(current, context, context.AuthorityGeneration.Epoch + 1, cancellationToken).ConfigureAwait(false))
                throw new InvalidOperationException("rollback-commit-rejected");
            _writes.CommitLegacy(context.AuthorityGeneration.Epoch + 1);
            stages.Add("rollback-authority-committed");
            ProductionAuthoritySnapshot after = await _authority.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (!AuthorityRoutingGuard.IsLegacyOperationalRoutingAllowed(after.Record) || after.Record.TargetAuthoritative || after.Record.TargetRoutingEnabled)
                throw new InvalidOperationException("rollback-postcondition-failed");
            await _audit.WriteAsync(new(context.CorrelationId, _clock.UtcNow.ToUniversalTime(), context.Scope.DeploymentScope, context.Scope.StationScope, context.ApplicationVersion,
                AuthorityState.TargetAuthoritative, AuthorityState.LegacyAuthoritative, AuthorityAuditAction.RollbackSucceeded, "SUCCEEDED", "rollback-complete", context.PrerequisiteReceipt.EvidenceReference, context.AuthorityGeneration.Epoch + 1, context.Scope.ProfileScope, context.ManagementCredentialProof.IsComplete ? "VALID" : "INVALID"), cancellationToken).ConfigureAwait(false);
            stages.Add("rollback-audit-finalized");
            return new(ProductionExecutionStatus.Succeeded, "rollback-complete", stages.AsReadOnly(), true, false);
        }
        catch (Exception ex)
        {
            await _authority.DisableTargetRoutingAsync(CancellationToken.None).ConfigureAwait(false);
            await _authority.EnterRecoveryAsync(context.CorrelationId, "rollback-outcome-unknown-" + ex.GetType().Name, context.AuthorityGeneration.Epoch + 1, CancellationToken.None).ConfigureAwait(false);
            _writes.AbortDrain();
            stages.Add("recovery-required");
            return new(ProductionExecutionStatus.RecoveryRequired, "rollback-outcome-unknown", stages.AsReadOnly(), false, false);
        }
        finally { await drain.DisposeAsync().ConfigureAwait(false); }
    }
}

/// <summary>
/// Append-only JSON-lines audit with sequence and chained SHA-256 evidence.
/// Retention is KeepAll: no application path prunes lifecycle evidence.
/// </summary>
public sealed class TamperEvidentAuthorityAuditSink : IAuthorityAuditSink, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() { Converters = { new JsonStringEnumConverter() }, WriteIndented = false };
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;
    public TamperEvidentAuthorityAuditSink(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Path.IsPathFullyQualified(path)) throw new ArgumentException("Audit path must be fully qualified.", nameof(path));
        _path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
    }
    public async Task WriteAsync(AuthorityAuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            AuditIntegrityResult integrity = await VerifyCoreAsync(cancellationToken).ConfigureAwait(false);
            if (!integrity.IsValid) throw new InvalidDataException("activation-audit-integrity-failed");
            long sequence = integrity.LastSequence + 1;
            string previous = integrity.LastDigest;
            var unsigned = new ChainedAuditRecord(sequence, previous, entry, string.Empty);
            string payload = JsonSerializer.Serialize(unsigned, JsonOptions);
            string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(previous + "|" + payload)));
            string line = JsonSerializer.Serialize(unsigned with { Digest = digest }, JsonOptions) + Environment.NewLine;
            await using var stream = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            await writer.WriteAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            stream.Flush(true);
        }
        finally { _gate.Release(); }
    }
    public async Task<AuditIntegrityResult> VerifyAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await VerifyCoreAsync(cancellationToken).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }
    private async Task<AuditIntegrityResult> VerifyCoreAsync(CancellationToken token)
    {
        if (!File.Exists(_path)) return new(true, 0, "GENESIS", Array.Empty<string>());
        long expected = 1; string previous = "GENESIS"; var issues = new List<string>();
        try
        {
            foreach (string line in await File.ReadAllLinesAsync(_path, token).ConfigureAwait(false))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                ChainedAuditRecord? item = JsonSerializer.Deserialize<ChainedAuditRecord>(line, JsonOptions);
                if (item is null || item.Sequence != expected || !StringComparer.Ordinal.Equals(item.PreviousDigest, previous)) { issues.Add("audit-sequence-or-link-invalid"); break; }
                string unsigned = JsonSerializer.Serialize(item with { Digest = string.Empty }, JsonOptions);
                string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(previous + "|" + unsigned)));
                if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(digest), Convert.FromHexString(item.Digest))) { issues.Add("audit-digest-invalid"); break; }
                previous = item.Digest; expected++;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or FormatException) { issues.Add("audit-unreadable"); }
        return new(issues.Count == 0, expected - 1, previous, issues.AsReadOnly());
    }
    public void Dispose() { if (!_disposed) { _disposed = true; _gate.Dispose(); } }
    private sealed record ChainedAuditRecord(long Sequence, string PreviousDigest, AuthorityAuditEntry Entry, string Digest);
}

public sealed record AuditIntegrityResult(bool IsValid, long LastSequence, string LastDigest, IReadOnlyList<string> Issues);
