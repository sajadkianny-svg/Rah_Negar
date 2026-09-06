using Rah_Negar.Foundation.Application.Authority;

namespace Rah_Negar.Tests.Authority;

public sealed class Phase96ERehearsalTests
{
    [Fact]
    public async Task Isolated_happy_path_commits_target_then_makes_routing_eligible_and_survives_restart()
    {
        using TempDirectory temp = new();
        var authority = new FileAuthorityStateStore(Path.Combine(temp.Path, "authority.json"));
        var transitions = new FileTransitionStateStore(Path.Combine(temp.Path, "transition.json"));
        var audit = new InMemoryAuthorityAuditSink();
        await authority.SaveAsync(AuthorityStateRecord.Legacy("qualification", "all"));
        RehearsalTransitionRequest request = Request("happy");
        RehearsalTransitionResult result = await new IsolatedRehearsalTransitionService(authority, transitions, audit).ExecuteAsync(request);

        Assert.Equal(RehearsalTransitionStatus.Succeeded, result.Status);
        Assert.Equal(AuthorityState.TargetAuthoritative, result.Authority.State);
        Assert.False(result.Authority.TargetRoutingEnabled);
        Assert.True(result.TargetRoutingEligible);
        AuthorityStartupResult restart = await new AuthorityStartupResolver(new FileAuthorityStateStore(Path.Combine(temp.Path, "authority.json")))
            .ResolveCanonicalAsync(new FileTransitionStateStore(Path.Combine(temp.Path, "transition.json")));
        Assert.Equal(AuthorityState.TargetAuthoritative, restart.EffectiveAuthority.State);
        Assert.Equal("CommitPersisted", restart.Classification);
        Assert.True(restart.RoutingBlocked);
        Assert.Contains(audit.Entries, e => e.Action == AuthorityAuditAction.CommitSucceeded);
    }

    [Fact]
    public async Task Production_context_is_rejected_and_invalid_prerequisites_are_fail_closed()
    {
        using TempDirectory temp = new();
        var authority = new FileAuthorityStateStore(Path.Combine(temp.Path, "authority.json"));
        var transitions = new FileTransitionStateStore(Path.Combine(temp.Path, "transition.json"));
        var service = new IsolatedRehearsalTransitionService(authority, transitions, new InMemoryAuthorityAuditSink());
        await authority.SaveAsync(AuthorityStateRecord.Legacy("qualification", "all"));
        Assert.Equal(RehearsalTransitionStatus.Rejected, (await service.ExecuteAsync(Request("prod") with { Context = RehearsalContext.Production })).Status);
        Assert.Equal(RehearsalTransitionStatus.Rejected, (await service.ExecuteAsync(Request("bad") with { Reconciliation = ReconciliationStatus.Mismatched })).Status);
        Assert.Equal(AuthorityState.LegacyAuthoritative, (await authority.LoadAsync()).Record.State);
        Assert.False((await authority.LoadAsync()).Record.TargetAuthoritative);
    }

    [Fact]
    public async Task Backup_scope_and_correlation_are_bound_and_stale_generation_is_fenced()
    {
        using TempDirectory temp = new();
        var authority = new FileAuthorityStateStore(Path.Combine(temp.Path, "authority.json"));
        var transitions = new FileTransitionStateStore(Path.Combine(temp.Path, "transition.json"));
        var service = new IsolatedRehearsalTransitionService(authority, transitions, new InMemoryAuthorityAuditSink());
        await authority.SaveAsync(AuthorityStateRecord.Legacy("qualification", "all"));
        Assert.Equal(RehearsalTransitionStatus.Rejected, (await service.ExecuteAsync(Request("wrong") with
        { ExpectedGeneration = 9, Backup = Backup("other", "wrong") })).Status);
        RehearsalTransitionResult first = await service.ExecuteAsync(Request("one"));
        RehearsalTransitionResult replay = await service.ExecuteAsync(Request("two") with { ExpectedGeneration = first.Authority.AuthorityEpoch });
        Assert.Equal(RehearsalTransitionStatus.Succeeded, first.Status);
        Assert.Equal(RehearsalTransitionStatus.Rejected, replay.Status);
    }

    [Fact]
    public async Task Audit_failure_enters_recovery_and_blocks_all_routes()
    {
        using TempDirectory temp = new();
        var authority = new FileAuthorityStateStore(Path.Combine(temp.Path, "authority.json"));
        var transitions = new FileTransitionStateStore(Path.Combine(temp.Path, "transition.json"));
        var audit = new FailureInjectingAuthorityAuditSink(new InMemoryAuthorityAuditSink()) { FailWrites = true };
        await authority.SaveAsync(AuthorityStateRecord.Legacy("qualification", "all"));
        RehearsalTransitionResult result = await new IsolatedRehearsalTransitionService(authority, transitions, audit).ExecuteAsync(Request("audit-fail"));
        Assert.Equal(RehearsalTransitionStatus.RecoveryRequired, result.Status);
        AuthorityStateRecord state = (await authority.LoadAsync()).Record;
        Assert.Equal(AuthorityState.RecoveryRequired, state.State);
        Assert.False(state.LegacyAuthoritative || state.TargetAuthoritative || state.TargetRoutingEnabled);
    }

    [Fact]
    public void Read_only_reconciliation_and_divergence_cover_all_rehearsal_outcomes()
    {
        var reconciliation = new ReadOnlyReconciliationEvaluator();
        var matching = new ReconciliationSnapshot(true, "same", 2, null);
        Assert.Equal(ReconciliationStatus.Matched, reconciliation.Evaluate(matching, matching).Status);
        Assert.Equal(ReconciliationStatus.Mismatched, reconciliation.Evaluate(matching, matching with { Fingerprint = "different" }).Status);
        Assert.Equal(ReconciliationStatus.NotEvaluated, reconciliation.Evaluate(matching, matching with { RecordCount = null }).Status);
        Assert.Equal(ReconciliationStatus.Blocked, reconciliation.Evaluate(matching, matching with { Readable = false }).Status);
        var divergence = new ReadOnlyDivergenceEvaluator();
        Assert.Equal(DivergenceStatus.Synchronized, divergence.Evaluate(new(true, false, false, false, false)).Status);
        Assert.Equal(DivergenceStatus.DeltaPending, divergence.Evaluate(new(true, true, false, false, false)).Status);
        Assert.Equal(DivergenceStatus.Diverged, divergence.Evaluate(new(true, false, false, false, true)).Status);
        Assert.Equal(DivergenceStatus.Blocked, divergence.Evaluate(new(false, false, false, false, false)).Status);
    }

    [Theory]
    [InlineData("after-intent")]
    [InlineData("after-preparation")]
    [InlineData("before-commit")]
    public async Task Abort_is_safe_at_each_precommit_point_and_retry_is_idempotent(string correlation)
    {
        using TempDirectory temp = new();
        var authority = new FileAuthorityStateStore(Path.Combine(temp.Path, "authority.json"));
        var transitions = new FileTransitionStateStore(Path.Combine(temp.Path, "transition.json"));
        var audit = new InMemoryAuthorityAuditSink();
        AuthorityStateRecord legacy = AuthorityStateRecord.Legacy("qualification", "all");
        await authority.SaveAsync(legacy);
        await transitions.SaveAsync(AuthorityTransitionRecord.PreparedFrom(legacy, correlation, 1));

        AuthorityAbortService service = new(authority, transitions, audit);
        AbortResult aborted = await service.AbortAsync(correlation, 1);
        Assert.Equal(AbortResultStatus.Aborted, aborted.Status);
        Assert.True(aborted.LegacyRemainsAuthoritative);
        Assert.False(aborted.TargetRoutingEnabled);
        Assert.Equal(TransitionLifecycle.Aborted, (await transitions.LoadAsync()).Record!.Lifecycle);

        AbortResult retry = await service.AbortAsync(correlation, 1);
        Assert.Equal(AbortResultStatus.AlreadyAborted, retry.Status);
        Assert.Equal(AuthorityState.LegacyAuthoritative, (await authority.LoadAsync()).Record.State);
        Assert.Contains(audit.Entries, e => e.Action == AuthorityAuditAction.Abort);
    }

    [Fact]
    public async Task Abort_rejects_wrong_correlation_and_stale_generation_without_mutation()
    {
        using TempDirectory temp = new();
        var authority = new FileAuthorityStateStore(Path.Combine(temp.Path, "authority.json"));
        var transitions = new FileTransitionStateStore(Path.Combine(temp.Path, "transition.json"));
        AuthorityStateRecord legacy = AuthorityStateRecord.Legacy("qualification", "all");
        await authority.SaveAsync(legacy);
        await transitions.SaveAsync(AuthorityTransitionRecord.PreparedFrom(legacy, "abort-guard", 2));
        AuthorityAbortService service = new(authority, transitions, new InMemoryAuthorityAuditSink());

        Assert.Equal(AbortResultStatus.Rejected, (await service.AbortAsync("wrong", 2)).Status);
        Assert.Equal(AbortResultStatus.Rejected, (await service.AbortAsync("abort-guard", 1)).Status);
        Assert.Equal(TransitionLifecycle.Prepared, (await transitions.LoadAsync()).Record!.Lifecycle);
        Assert.Equal(AuthorityState.LegacyAuthoritative, (await authority.LoadAsync()).Record.State);
    }

    [Fact]
    public async Task Safe_rollback_restores_legacy_and_unsafe_rollback_is_rejected()
    {
        using TempDirectory temp = new();
        var authority = new FileAuthorityStateStore(Path.Combine(temp.Path, "authority.json"));
        var transitions = new FileTransitionStateStore(Path.Combine(temp.Path, "transition.json"));
        var audit = new InMemoryAuthorityAuditSink();
        await authority.SaveAsync(AuthorityStateRecord.Legacy("qualification", "all"));
        RehearsalTransitionResult handoff = await new IsolatedRehearsalTransitionService(authority, transitions, audit)
            .ExecuteAsync(Request("rollback-safe"));
        Assert.Equal(RehearsalTransitionStatus.Succeeded, handoff.Status);

        RollbackSafetyEvidence safe = new(true, false, false, ReconciliationStatus.Matched, false, false,
            true, true, true, AuthorityState.TargetAuthoritative, 1, 1);
        RehearsalRollbackResult rollback = await new RehearsalRollbackService(authority, transitions, audit)
            .RollbackAsync(RehearsalContext.Qualification, "rollback-safe", 1, safe);
        Assert.Equal(RehearsalRollbackStatus.Succeeded, rollback.Status);
        Assert.Equal(AuthorityState.LegacyAuthoritative, rollback.Authority.State);
        Assert.True(AuthorityRoutingGuard.IsLegacyOperationalRoutingAllowed(rollback.Authority));
        Assert.False(rollback.Authority.TargetRoutingEnabled);

        await authority.SaveAsync(AuthorityStateRecord.Legacy("qualification", "all") with { AuthorityEpoch = 1, Revision = 2 });
        RehearsalTransitionResult unsafeHandoff = await new IsolatedRehearsalTransitionService(authority, transitions, audit)
            .ExecuteAsync(Request("rollback-unsafe") with { ExpectedGeneration = 2 });
        Assert.Equal(RehearsalTransitionStatus.Succeeded, unsafeHandoff.Status);
        RollbackSafetyEvidence unsafeEvidence = safe with { TargetAuthoritativeWrites = true, AuthorityEpoch = 2, TransitionGeneration = 2 };
        RehearsalRollbackResult rejected = await new RehearsalRollbackService(authority, transitions, audit)
            .RollbackAsync(RehearsalContext.Qualification, "rollback-unsafe", 2, unsafeEvidence);
        Assert.Equal(RehearsalRollbackStatus.Rejected, rejected.Status);
        Assert.Equal(AuthorityState.TargetAuthoritative, (await authority.LoadAsync()).Record.State);
    }

    [Fact]
    public async Task Rollback_persistence_failure_enters_recovery_and_restart_blocks_routes()
    {
        using TempDirectory temp = new();
        string authorityPath = Path.Combine(temp.Path, "authority.json");
        var authority = new FileAuthorityStateStore(authorityPath);
        var transitions = new FileTransitionStateStore(Path.Combine(temp.Path, "transition.json"));
        var audit = new InMemoryAuthorityAuditSink();
        await authority.SaveAsync(AuthorityStateRecord.Legacy("qualification", "all"));
        Assert.Equal(RehearsalTransitionStatus.Succeeded,
            (await new IsolatedRehearsalTransitionService(authority, transitions, audit).ExecuteAsync(Request("rollback-fail"))).Status);

        var failingTransitions = new FailingTransitionStore(transitions);
        RollbackSafetyEvidence safe = new(true, false, false, ReconciliationStatus.Matched, false, false,
            true, true, true, AuthorityState.TargetAuthoritative, 1, 1);
        RehearsalRollbackResult result = await new RehearsalRollbackService(authority, failingTransitions, audit)
            .RollbackAsync(RehearsalContext.Qualification, "rollback-fail", 1, safe);
        Assert.Equal(RehearsalRollbackStatus.RecoveryRequired, result.Status);
        AuthorityStartupResult restart = await new AuthorityStartupResolver(new FileAuthorityStateStore(authorityPath))
            .ResolveCanonicalAsync(new FileTransitionStateStore(Path.Combine(temp.Path, "transition.json")));
        Assert.Equal(AuthorityState.RecoveryRequired, restart.EffectiveAuthority.State);
        Assert.True(restart.RoutingBlocked);
        Assert.False(AuthorityRoutingGuard.IsTargetOperationalRoutingAllowed(restart.EffectiveAuthority));
    }

    [Fact]
    public async Task Failure_matrix_and_startup_metadata_are_fail_closed()
    {
        using TempDirectory temp = new();
        string authorityPath = Path.Combine(temp.Path, "authority.json");
        string transitionPath = Path.Combine(temp.Path, "transition.json");
        await File.WriteAllTextAsync(authorityPath, "{not-json");
        AuthorityStartupResult malformed = await new AuthorityStartupResolver(new FileAuthorityStateStore(authorityPath))
            .ResolveCanonicalAsync(new FileTransitionStateStore(transitionPath));
        Assert.True(malformed.RoutingBlocked);
        Assert.Equal("InvalidOrCorrupt", malformed.Classification);
        Assert.NotEmpty(RecoveryOperatorMessage.Persian(malformed.Classification, malformed.Issues));

        await File.WriteAllTextAsync(authorityPath, "{}");
        await File.WriteAllTextAsync(transitionPath, "{not-json");
        AuthorityStartupResult corruptTransition = await new AuthorityStartupResolver(new FileAuthorityStateStore(authorityPath))
            .ResolveCanonicalAsync(new FileTransitionStateStore(transitionPath));
        Assert.True(corruptTransition.RoutingBlocked);
        Assert.False(AuthorityRoutingGuard.IsTargetOperationalRoutingAllowed(corruptTransition.EffectiveAuthority));
    }

    private static RehearsalTransitionRequest Request(string correlation) => new(
        RehearsalContext.Qualification, correlation, "qualification", "all", "9.6E", 1,
        ReconciliationStatus.Matched, DivergenceStatus.Synchronized, Backup("qualification", correlation));

    private static VerifiedBackupEvidence Backup(string scope, string correlation) => new(
        true, "legacy.sqlite", "legacy.backup.sqlite", "A", "B", "delete", true, true, false,
        scope, "1", correlation, DateTimeOffset.UtcNow, []);

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory() { Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rah-96e-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Path); }
        public string Path { get; }
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); }
    }

    private sealed class FailingTransitionStore : ITransitionStateStore
    {
        private readonly ITransitionStateStore _inner;
        public FailingTransitionStore(ITransitionStateStore inner) => _inner = inner;
        public Task<TransitionLoadResult> LoadAsync(CancellationToken cancellationToken = default) => _inner.LoadAsync(cancellationToken);
        public Task SaveAsync(AuthorityTransitionRecord record, CancellationToken cancellationToken = default) =>
            throw new IOException("Injected rollback persistence failure.");
        public Task ClearAsync(CancellationToken cancellationToken = default) => _inner.ClearAsync(cancellationToken);
    }
}
