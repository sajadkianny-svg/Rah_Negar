using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rah_Negar.Foundation.Application.Authority;

public enum AuthorityState
{
    LegacyAuthoritative,
    ActivationPreparedNotExecuted,
    TransitionInProgress,
    TargetAuthoritative,
    RollbackInProgress,
    RecoveryRequired
}

public enum AuthorityLoadStatus { Loaded, Initialized, RecoveryRequired }

public sealed record AuthorityStateRecord(
    int SchemaVersion,
    long Revision,
    AuthorityState State,
    bool LegacyAuthoritative,
    bool TargetAuthoritative,
    bool TargetRoutingEnabled,
    string DeploymentScope,
    string StationScope,
    string? CorrelationId,
    string? Reason,
    DateTimeOffset RecordedAtUtc)
{
    // D2 fencing identity. Kept additive so the D1 persisted contract remains readable.
    public long AuthorityEpoch { get; init; }
    public static AuthorityStateRecord Legacy(string deploymentScope = "production", string stationScope = "all") =>
        new(1, 0, AuthorityState.LegacyAuthoritative, true, false, false,
            Required(deploymentScope, nameof(deploymentScope)), Required(stationScope, nameof(stationScope)),
            null, null, DateTimeOffset.UtcNow);

    public static AuthorityStateRecord Recovery(string reason, string correlationId = "authority-recovery") =>
        new(1, 0, AuthorityState.RecoveryRequired, false, false, false, "unknown", "unknown",
            Required(correlationId, nameof(correlationId)), Required(reason, nameof(reason)), DateTimeOffset.UtcNow);

    private static string Required(string value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", name) : value.Trim();
}

public sealed record AuthorityValidationResult(bool IsValid, IReadOnlyList<string> Issues)
{
    public static AuthorityValidationResult Valid() => new(true, Array.Empty<string>());
}

public static class AuthorityStateValidator
{
    public static AuthorityValidationResult Validate(AuthorityStateRecord? record)
    {
        if (record is null) return Invalid("authority-record-missing");
        var issues = new List<string>();
        if (record.SchemaVersion != 1) issues.Add("authority-schema-unsupported");
        if (record.Revision < 0) issues.Add("authority-revision-invalid");
        if (record.RecordedAtUtc.Offset != TimeSpan.Zero) issues.Add("authority-time-not-utc");
        if (string.IsNullOrWhiteSpace(record.DeploymentScope) || string.IsNullOrWhiteSpace(record.StationScope))
            issues.Add("authority-scope-missing");
        bool correlationRequired = record.State != AuthorityState.LegacyAuthoritative;
        if (correlationRequired && string.IsNullOrWhiteSpace(record.CorrelationId))
            issues.Add("authority-correlation-missing");
        if (record.State == AuthorityState.RecoveryRequired && string.IsNullOrWhiteSpace(record.Reason))
            issues.Add("authority-recovery-reason-missing");

        bool expectedLegacy = record.State is AuthorityState.LegacyAuthoritative or
            AuthorityState.ActivationPreparedNotExecuted or AuthorityState.TransitionInProgress;
        bool expectedTarget = record.State == AuthorityState.TargetAuthoritative;
        bool expectedRoute = record.State == AuthorityState.TargetAuthoritative && record.TargetRoutingEnabled;
        if (record.LegacyAuthoritative != expectedLegacy || record.TargetAuthoritative != expectedTarget)
            issues.Add("authority-flags-contradict-state");
        if (record.TargetRoutingEnabled != expectedRoute)
            issues.Add("authority-route-contradicts-state");
        if (record.TargetAuthoritative && record.LegacyAuthoritative)
            issues.Add("authority-dual-authority");
        if (record.TargetRoutingEnabled && !record.TargetAuthoritative)
            issues.Add("authority-target-route-without-authority");
        return issues.Count == 0 ? AuthorityValidationResult.Valid() :
            new(false, new ReadOnlyCollection<string>(issues));
    }

    private static AuthorityValidationResult Invalid(string issue) => new(false, [issue]);
}

public sealed record AuthorityLoadResult(
    AuthorityLoadStatus Status,
    AuthorityStateRecord Record,
    IReadOnlyList<string> Issues)
{
    public bool IsOperationallySafe => Status != AuthorityLoadStatus.RecoveryRequired &&
        AuthorityStateValidator.Validate(Record).IsValid;
}

public interface IAuthorityStateStore
{
    Task<AuthorityLoadResult> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AuthorityStateRecord record, CancellationToken cancellationToken = default);
}

public sealed class FileAuthorityStateStore : IAuthorityStateStore
{
    private readonly string _path;
    private readonly Func<bool>? _failWrite;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public FileAuthorityStateStore(string path, Func<bool>? failWrite = null)
    {
        _path = string.IsNullOrWhiteSpace(path) ? throw new ArgumentException("Path is required.", nameof(path)) : path;
        _failWrite = failWrite;
    }

    public async Task<AuthorityLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
        {
            var initial = AuthorityStateRecord.Legacy();
            return new(AuthorityLoadStatus.Initialized, initial, Array.Empty<string>());
        }
        try
        {
            string text = await File.ReadAllTextAsync(_path, cancellationToken).ConfigureAwait(false);
            var envelope = JsonSerializer.Deserialize<AuthorityEnvelope>(text, JsonOptions);
            if (envelope is null || string.IsNullOrWhiteSpace(envelope.Payload) || string.IsNullOrWhiteSpace(envelope.IntegritySha256))
                return Recovery("authority-metadata-malformed");
            string actual = Hash(envelope.Payload);
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(actual), Convert.FromHexString(envelope.IntegritySha256)))
                return Recovery("authority-metadata-integrity-failed");
            var record = JsonSerializer.Deserialize<AuthorityStateRecord>(envelope.Payload, JsonOptions);
            AuthorityValidationResult validation = AuthorityStateValidator.Validate(record);
            return validation.IsValid ? new(AuthorityLoadStatus.Loaded, record!, Array.Empty<string>()) :
                new(AuthorityLoadStatus.RecoveryRequired, AuthorityStateRecord.Recovery(string.Join(';', validation.Issues)), validation.Issues);
        }
        catch (Exception ex) when (ex is JsonException or FormatException or IOException or UnauthorizedAccessException)
        {
            return Recovery("authority-metadata-unreadable");
        }
    }

    public async Task SaveAsync(AuthorityStateRecord record, CancellationToken cancellationToken = default)
    {
        AuthorityValidationResult validation = AuthorityStateValidator.Validate(record);
        if (!validation.IsValid) throw new InvalidOperationException(string.Join(';', validation.Issues));
        if (_failWrite?.Invoke() == true) throw new IOException("Injected authority persistence failure.");
        string payload = JsonSerializer.Serialize(record, JsonOptions);
        string text = JsonSerializer.Serialize(new AuthorityEnvelope(payload, Hash(payload)), JsonOptions);
        string? directory = Path.GetDirectoryName(Path.GetFullPath(_path));
        if (directory is not null) Directory.CreateDirectory(directory);
        string temp = _path + ".tmp-" + Guid.NewGuid().ToString("N");
        await File.WriteAllTextAsync(temp, text, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        File.Move(temp, _path, true);
    }

    private static AuthorityLoadResult Recovery(string reason) =>
        new(AuthorityLoadStatus.RecoveryRequired, AuthorityStateRecord.Recovery(reason), [reason]);
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private sealed record AuthorityEnvelope(string Payload, string IntegritySha256);
}

public enum TransitionIntentStatus { Created, ValidationRejected, Completed, Aborted }

public sealed record TransitionIntent(
    string CorrelationId,
    string DeploymentScope,
    string StationScope,
    string ApplicationVersion,
    AuthorityState SourceState,
    AuthorityState RequestedTargetState,
    DateTimeOffset CreatedAtUtc,
    TransitionIntentStatus Status,
    string? EvidenceReference,
    string? Reason)
{
    public bool ChangesAuthority => false;
    public bool EnablesRouting => false;
}

public sealed record TransitionIntentValidationResult(bool IsValid, IReadOnlyList<string> Issues);

public static class TransitionIntentValidator
{
    public static TransitionIntentValidationResult Validate(TransitionIntent intent, AuthorityStateRecord current)
    {
        var issues = new List<string>();
        if (intent is null) issues.Add("intent-missing");
        else
        {
            if (string.IsNullOrWhiteSpace(intent.CorrelationId)) issues.Add("intent-correlation-missing");
            if (string.IsNullOrWhiteSpace(intent.DeploymentScope) || string.IsNullOrWhiteSpace(intent.StationScope)) issues.Add("intent-scope-missing");
            if (string.IsNullOrWhiteSpace(intent.ApplicationVersion) || intent.ApplicationVersion.Any(char.IsWhiteSpace)) issues.Add("intent-version-invalid");
            if (intent.CreatedAtUtc.Offset != TimeSpan.Zero) issues.Add("intent-time-not-utc");
            if (intent.SourceState != current.State) issues.Add("intent-source-state-mismatch");
            if (intent.RequestedTargetState is AuthorityState.TargetAuthoritative or AuthorityState.RollbackInProgress)
                issues.Add("intent-target-transition-not-supported");
            if (intent.SourceState == intent.RequestedTargetState) issues.Add("intent-transition-unchanged");
            if (!StringComparer.Ordinal.Equals(intent.DeploymentScope, current.DeploymentScope) ||
                !StringComparer.Ordinal.Equals(intent.StationScope, current.StationScope)) issues.Add("intent-scope-mismatch");
        }
        return new(issues.Count == 0, new ReadOnlyCollection<string>(issues));
    }
}

public interface ITransitionIntentStore
{
    Task<bool> ContainsCorrelationAsync(string correlationId, CancellationToken cancellationToken = default);
    Task SaveAsync(TransitionIntent intent, CancellationToken cancellationToken = default);
}

public sealed class InMemoryTransitionIntentStore : ITransitionIntentStore
{
    private readonly Dictionary<string, TransitionIntent> _items = new(StringComparer.Ordinal);
    public Task<bool> ContainsCorrelationAsync(string correlationId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_items.ContainsKey(correlationId));
    public Task SaveAsync(TransitionIntent intent, CancellationToken cancellationToken = default)
    {
        if (_items.ContainsKey(intent.CorrelationId)) throw new InvalidOperationException("Transition correlation was already used.");
        _items.Add(intent.CorrelationId, intent);
        return Task.CompletedTask;
    }
}

public sealed class TransitionIntentService
{
    private readonly ITransitionIntentStore _store;
    public TransitionIntentService(ITransitionIntentStore store) => _store = store ?? throw new ArgumentNullException(nameof(store));
    public async Task<(TransitionIntent? Intent, TransitionIntentValidationResult Validation)> CreateAsync(
        TransitionIntent intent, AuthorityStateRecord current, CancellationToken cancellationToken = default)
    {
        TransitionIntentValidationResult validation = TransitionIntentValidator.Validate(intent, current);
        if (intent is null || !validation.IsValid || await _store.ContainsCorrelationAsync(intent.CorrelationId, cancellationToken).ConfigureAwait(false))
            return (null, new(false, validation.Issues.Concat(["intent-correlation-replayed"]).ToArray()));
        await _store.SaveAsync(intent, cancellationToken).ConfigureAwait(false);
        return (intent, validation);
    }
}

public enum AuthorityAuditAction { TransitionIntentCreated, ValidationFailed, RecoveryRequiredEntered, InvalidTransitionRejected }
public sealed record AuthorityAuditEntry(
    string CorrelationId, DateTimeOffset TimestampUtc, string DeploymentScope, string StationScope,
    string ApplicationVersion, AuthorityState? PreviousState, AuthorityState? RequestedState,
    AuthorityAuditAction Action, string Result, string Reason);

public interface IAuthorityAuditSink { Task WriteAsync(AuthorityAuditEntry entry, CancellationToken cancellationToken = default); }
public sealed class FileAuthorityAuditSink : IAuthorityAuditSink
{
    private readonly string _path;
    private readonly Func<bool>? _failWrite;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    public FileAuthorityAuditSink(string path, Func<bool>? failWrite = null)
    {
        _path = string.IsNullOrWhiteSpace(path) ? throw new ArgumentException("Path is required.", nameof(path)) : path;
        _failWrite = failWrite;
    }
    public async Task WriteAsync(AuthorityAuditEntry entry, CancellationToken cancellationToken = default)
    {
        if (_failWrite?.Invoke() == true) throw new IOException("Injected audit persistence failure.");
        string? directory = Path.GetDirectoryName(Path.GetFullPath(_path));
        if (directory is not null) Directory.CreateDirectory(directory);
        await File.AppendAllTextAsync(_path, JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine,
            Encoding.UTF8, cancellationToken).ConfigureAwait(false);
    }
}
public sealed class InMemoryAuthorityAuditSink : IAuthorityAuditSink
{
    private readonly List<AuthorityAuditEntry> _entries = [];
    public IReadOnlyList<AuthorityAuditEntry> Entries => _entries.AsReadOnly();
    public Task WriteAsync(AuthorityAuditEntry entry, CancellationToken cancellationToken = default) { _entries.Add(entry); return Task.CompletedTask; }
}

public sealed class FailureInjectingAuthorityAuditSink : IAuthorityAuditSink
{
    private readonly IAuthorityAuditSink _inner;
    public bool FailWrites { get; set; }
    public FailureInjectingAuthorityAuditSink(IAuthorityAuditSink inner) => _inner = inner;
    public Task WriteAsync(AuthorityAuditEntry entry, CancellationToken cancellationToken = default) =>
        FailWrites ? throw new IOException("Injected audit persistence failure.") : _inner.WriteAsync(entry, cancellationToken);
}

public static class AuthorityRoutingGuard
{
    public static bool IsTargetOperationalRoutingAllowed(AuthorityStateRecord record) =>
        AuthorityStateValidator.Validate(record).IsValid && record.State == AuthorityState.TargetAuthoritative && record.TargetRoutingEnabled;
    public static bool IsLegacyOperationalRoutingAllowed(AuthorityStateRecord record) =>
        AuthorityStateValidator.Validate(record).IsValid && record.State == AuthorityState.LegacyAuthoritative && record.LegacyAuthoritative;
}

public enum ReconciliationStatus { NotEvaluated, Matched, Mismatched, Blocked }
public sealed record ReconciliationSnapshot(bool Readable, string? Fingerprint, long? RecordCount, string? Reason);
public sealed record ReconciliationResult(ReconciliationStatus Status, string Reason, bool MutatedAuthority, bool MutatedRouting, bool MutatedData);
public interface IReadOnlyReconciliationEvaluator { ReconciliationResult Evaluate(ReconciliationSnapshot legacy, ReconciliationSnapshot target); }
public sealed class ReadOnlyReconciliationEvaluator : IReadOnlyReconciliationEvaluator
{
    public ReconciliationResult Evaluate(ReconciliationSnapshot legacy, ReconciliationSnapshot target)
    {
        if (!legacy.Readable || !target.Readable) return new(ReconciliationStatus.Blocked, "reconciliation-source-unreadable", false, false, false);
        if (legacy.Fingerprint is null || target.Fingerprint is null || legacy.RecordCount is null || target.RecordCount is null)
            return new(ReconciliationStatus.NotEvaluated, "reconciliation-not-evaluated", false, false, false);
        return StringComparer.OrdinalIgnoreCase.Equals(legacy.Fingerprint, target.Fingerprint) && legacy.RecordCount == target.RecordCount
            ? new(ReconciliationStatus.Matched, "reconciliation-matched", false, false, false)
            : new(ReconciliationStatus.Mismatched, "reconciliation-mismatched", false, false, false);
    }
}

public sealed class AuthorityStartupResolver
{
    private readonly IAuthorityStateStore _store;
    public AuthorityStartupResolver(IAuthorityStateStore store) => _store = store ?? throw new ArgumentNullException(nameof(store));
    public async Task<AuthorityStartupResult> ResolveCanonicalAsync(
        ITransitionStateStore? transitionStore = null, CancellationToken cancellationToken = default)
    {
        AuthorityLoadResult authority = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (transitionStore is null)
            return AuthorityStartupResult.FromAuthority(authority);

        TransitionLoadResult transition = await transitionStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        return AuthorityStartupResult.Resolve(authority, transition);
    }

    public Task<AuthorityLoadResult> ResolveAsync(CancellationToken cancellationToken = default) => _store.LoadAsync(cancellationToken);
}

public enum TransitionLifecycle { Idle, Prepared, Committing, Committed }
public enum TransitionLoadStatus { Missing, Loaded, RecoveryRequired }

public sealed record AuthorityTransitionRecord(
    string TransitionId, long Generation, AuthorityState SourceState,
    AuthorityState RequestedState, TransitionLifecycle Lifecycle,
    DateTimeOffset RecordedAtUtc, string DeploymentScope, string StationScope)
{
    public static AuthorityTransitionRecord PreparedFrom(AuthorityStateRecord current, string transitionId, long generation) =>
        new(Required(transitionId, nameof(transitionId)), generation, current.State,
            AuthorityState.ActivationPreparedNotExecuted, TransitionLifecycle.Prepared,
            DateTimeOffset.UtcNow, current.DeploymentScope, current.StationScope);

    private static string Required(string value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", name) : value.Trim();
}

public sealed record TransitionLoadResult(TransitionLoadStatus Status, AuthorityTransitionRecord? Record, IReadOnlyList<string> Issues);
public interface ITransitionStateStore
{
    Task<TransitionLoadResult> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AuthorityTransitionRecord record, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}

public sealed class FileTransitionStateStore : ITransitionStateStore
{
    private readonly string _path;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    public FileTransitionStateStore(string path) => _path = string.IsNullOrWhiteSpace(path) ? throw new ArgumentException("Path is required.", nameof(path)) : path;

    public async Task<TransitionLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path)) return new(TransitionLoadStatus.Missing, null, Array.Empty<string>());
        try
        {
            string text = await File.ReadAllTextAsync(_path, cancellationToken).ConfigureAwait(false);
            var envelope = JsonSerializer.Deserialize<TransitionEnvelope>(text, JsonOptions);
            if (envelope is null || string.IsNullOrWhiteSpace(envelope.Payload) || string.IsNullOrWhiteSpace(envelope.IntegritySha256))
                return Recovery("transition-metadata-malformed");
            if (!StringComparer.OrdinalIgnoreCase.Equals(Hash(envelope.Payload), envelope.IntegritySha256))
                return Recovery("transition-metadata-integrity-failed");
            var record = JsonSerializer.Deserialize<AuthorityTransitionRecord>(envelope.Payload, JsonOptions);
            if (record is null || record.Generation < 1 || record.RecordedAtUtc.Offset != TimeSpan.Zero || string.IsNullOrWhiteSpace(record.TransitionId))
                return Recovery("transition-metadata-invalid");
            return new(TransitionLoadStatus.Loaded, record, Array.Empty<string>());
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        { return Recovery("transition-metadata-unreadable"); }
    }

    public async Task SaveAsync(AuthorityTransitionRecord record, CancellationToken cancellationToken = default)
    {
        string payload = JsonSerializer.Serialize(record, JsonOptions);
        string text = JsonSerializer.Serialize(new TransitionEnvelope(payload, Hash(payload)), JsonOptions);
        string? directory = Path.GetDirectoryName(Path.GetFullPath(_path));
        if (directory is not null) Directory.CreateDirectory(directory);
        string temp = _path + ".tmp-" + Guid.NewGuid().ToString("N");
        await File.WriteAllTextAsync(temp, text, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        File.Move(temp, _path, true);
    }
    public Task ClearAsync(CancellationToken cancellationToken = default) { if (File.Exists(_path)) File.Delete(_path); return Task.CompletedTask; }
    private static TransitionLoadResult Recovery(string reason) => new(TransitionLoadStatus.RecoveryRequired, null, [reason]);
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private sealed record TransitionEnvelope(string Payload, string IntegritySha256);
}

public sealed record AuthorityStartupResult(
    AuthorityLoadResult Authority, TransitionLoadResult Transition, bool RoutingBlocked,
    string Classification, IReadOnlyList<string> Issues)
{
    public AuthorityStateRecord EffectiveAuthority => Authority.Record;
    public static AuthorityStartupResult FromAuthority(AuthorityLoadResult authority) =>
        new(authority, new(TransitionLoadStatus.Missing, null, Array.Empty<string>()),
            authority.Status == AuthorityLoadStatus.RecoveryRequired, authority.Status == AuthorityLoadStatus.RecoveryRequired ? "InvalidAuthority" : "CleanIdle", authority.Issues);
    public static AuthorityStartupResult Resolve(AuthorityLoadResult authority, TransitionLoadResult transition)
    {
        var issues = authority.Issues.Concat(transition.Issues).ToArray();
        if (authority.Status == AuthorityLoadStatus.RecoveryRequired || transition.Status == TransitionLoadStatus.RecoveryRequired)
            return new(authority, transition, true, "InvalidOrCorrupt", issues);
        if (transition.Record is null) return new(authority, transition, false, "CleanIdle", issues);
        AuthorityTransitionRecord t = transition.Record;
        bool consistent = t.Generation >= authority.Record.AuthorityEpoch &&
            t.SourceState == authority.Record.State &&
            t.DeploymentScope == authority.Record.DeploymentScope && t.StationScope == authority.Record.StationScope;
        if (!consistent) return new(authority, transition, true, "AuthorityTransitionMismatch", [.. issues, "authority-transition-mismatch"]);
        return new(authority, transition, true, t.Lifecycle switch
        {
            TransitionLifecycle.Prepared => "PreparedNotCommitted",
            TransitionLifecycle.Committing => "CommitInProgress",
            TransitionLifecycle.Committed => "CommitPersisted",
            _ => "InvalidOrCorrupt"
        }, issues);
    }
}

public sealed class AuthorityCommitService
{
    private readonly IAuthorityStateStore _authority;
    private readonly ITransitionStateStore _transitions;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    public AuthorityCommitService(IAuthorityStateStore authority, ITransitionStateStore transitions) => (_authority, _transitions) =
        (authority ?? throw new ArgumentNullException(nameof(authority)), transitions ?? throw new ArgumentNullException(nameof(transitions)));

    public async Task<AuthorityTransitionRecord> PrepareAsync(string transitionId, CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AuthorityLoadResult current = await _authority.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (!current.IsOperationallySafe) throw new InvalidOperationException("Authority state is not operationally safe.");
            TransitionLoadResult existing = await _transitions.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (existing.Record is { } prior && prior.TransitionId == transitionId) return prior;
            if (existing.Record is { Lifecycle: not TransitionLifecycle.Committed }) throw new InvalidOperationException("Another transition is active.");
            long generation = Math.Max(current.Record.AuthorityEpoch, existing.Record?.Generation ?? 0) + 1;
            var prepared = AuthorityTransitionRecord.PreparedFrom(current.Record, transitionId, generation);
            await _transitions.SaveAsync(prepared, cancellationToken).ConfigureAwait(false);
            return prepared;
        }
        finally { _mutex.Release(); }
    }

    public async Task<bool> CommitAsync(string transitionId, long generation, CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AuthorityLoadResult current = await _authority.LoadAsync(cancellationToken).ConfigureAwait(false);
            TransitionLoadResult loaded = await _transitions.LoadAsync(cancellationToken).ConfigureAwait(false);
            AuthorityTransitionRecord t = loaded.Record ?? throw new InvalidOperationException("Transition is missing.");
            if (t.TransitionId != transitionId || t.Generation != generation) throw new InvalidOperationException("Stale transition fenced.");
            if (t.Lifecycle == TransitionLifecycle.Committed) return true;
            if (t.SourceState != current.Record.State) throw new InvalidOperationException("Stale authority state fenced.");
            if (t.Lifecycle == TransitionLifecycle.Committing && current.Record.AuthorityEpoch == generation)
            {
                await _transitions.SaveAsync(t with { Lifecycle = TransitionLifecycle.Committed, RecordedAtUtc = DateTimeOffset.UtcNow }, cancellationToken).ConfigureAwait(false);
                return true;
            }
            if (generation <= current.Record.AuthorityEpoch) throw new InvalidOperationException("Stale authority epoch fenced.");
            if (t.Lifecycle is not (TransitionLifecycle.Prepared or TransitionLifecycle.Committing)) throw new InvalidOperationException("Transition is not committable.");
            await _transitions.SaveAsync(t with { Lifecycle = TransitionLifecycle.Committing, RecordedAtUtc = DateTimeOffset.UtcNow }, cancellationToken).ConfigureAwait(false);
            // D2 commit changes only the canonical transition marker; Legacy remains authoritative and routing stays disabled.
            await _authority.SaveAsync(current.Record with { AuthorityEpoch = generation, Revision = current.Record.Revision + 1 }, cancellationToken).ConfigureAwait(false);
            await _transitions.SaveAsync(t with { Lifecycle = TransitionLifecycle.Committed, RecordedAtUtc = DateTimeOffset.UtcNow }, cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally { _mutex.Release(); }
    }
}
