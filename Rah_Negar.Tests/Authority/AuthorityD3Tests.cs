using Rah_Negar.Foundation.Application.Authority;

namespace Rah_Negar.Tests.Authority;

public sealed class AuthorityD3Tests
{
    [Fact]
    public void Structured_reconciliation_reports_component_reasons_and_never_mutates()
    {
        var evaluator = new LegacyTargetReconciliationEvaluator();
        StructuredReconciliationResult result = evaluator.Evaluate([
            new(ReconciliationComponentCategory.Schema, true, "schema-a", "schema-b"),
            new(ReconciliationComponentCategory.EventsRuntime, false, null, null, "source-unavailable")]);
        Assert.Equal(ReconciliationStatus.Blocked, result.Status);
        Assert.Contains(result.MismatchReasons, x => x.Contains("Schema", StringComparison.Ordinal));
        Assert.Contains(result.UnavailableEvidence, x => x.Contains("EventsRuntime", StringComparison.Ordinal));
        Assert.True(result.IsReadOnly);
    }

    [Fact]
    public void Divergence_evaluator_distinguishes_synchronized_delta_and_conflict()
    {
        var evaluator = new ReadOnlyDivergenceEvaluator();
        Assert.Equal(DivergenceStatus.Synchronized, evaluator.Evaluate(new(true, false, false, false, false)).Status);
        Assert.Equal(DivergenceStatus.DeltaPending, evaluator.Evaluate(new(true, true, true, true, false)).Status);
        Assert.Equal(DivergenceStatus.Diverged, evaluator.Evaluate(new(true, false, false, false, true)).Status);
        Assert.Equal(DivergenceStatus.Blocked, evaluator.Evaluate(new(false, false, false, false, false, "missing")).Status);
    }

    [Fact]
    public async Task Abort_is_correlated_idempotent_and_survives_restart()
    {
        using TempDirectory temp = new();
        string authorityPath = Path.Combine(temp.Path, "authority.json");
        string transitionPath = Path.Combine(temp.Path, "transition.json");
        var authority = new FileAuthorityStateStore(authorityPath);
        var transitions = new FileTransitionStateStore(transitionPath);
        var audit = new InMemoryAuthorityAuditSink();
        var prepared = await new AuthorityCommitService(authority, transitions).PrepareAsync("abort-1");
        var service = new AuthorityAbortService(authority, transitions, audit);

        AbortResult first = await service.AbortAsync("abort-1", prepared.Generation);
        AbortResult second = await service.AbortAsync("abort-1", prepared.Generation);
        Assert.Equal(AbortResultStatus.Aborted, first.Status);
        Assert.Equal(AbortResultStatus.AlreadyAborted, second.Status);
        Assert.Equal(TransitionLifecycle.Aborted, (await new FileTransitionStateStore(transitionPath).LoadAsync()).Record!.Lifecycle);
        Assert.True((await new FileAuthorityStateStore(authorityPath).LoadAsync()).Record.LegacyAuthoritative);
        Assert.False((await new FileAuthorityStateStore(authorityPath).LoadAsync()).Record.TargetRoutingEnabled);
        AuthorityStartupResult startup = await new AuthorityStartupResolver(new FileAuthorityStateStore(authorityPath))
            .ResolveCanonicalAsync(new FileTransitionStateStore(transitionPath));
        Assert.False(startup.RoutingBlocked);
        Assert.Equal("Aborted", startup.Classification);
        Assert.Contains(audit.Entries, x => x.Action == AuthorityAuditAction.Abort);
    }

    [Fact]
    public async Task Abort_rejects_wrong_or_stale_correlation_and_audit_failure_enters_recovery()
    {
        using TempDirectory temp = new();
        var authority = new FileAuthorityStateStore(Path.Combine(temp.Path, "authority.json"));
        var transitions = new FileTransitionStateStore(Path.Combine(temp.Path, "transition.json"));
        var prepared = await new AuthorityCommitService(authority, transitions).PrepareAsync("abort-2");
        var service = new AuthorityAbortService(authority, transitions,
            new FailureInjectingAuthorityAuditSink(new InMemoryAuthorityAuditSink()) { FailWrites = true });
        Assert.Equal(AbortResultStatus.Rejected, (await service.AbortAsync("other", prepared.Generation)).Status);
        Assert.Equal(AbortResultStatus.Rejected, (await service.AbortAsync("abort-2", prepared.Generation - 1)).Status);
        Assert.Equal(AbortResultStatus.RecoveryRequired, (await service.AbortAsync("abort-2", prepared.Generation)).Status);
        Assert.Equal(AuthorityState.RecoveryRequired, (await authority.LoadAsync()).Record.State);
    }

    [Fact]
    public async Task Abort_persistence_failure_is_fail_closed()
    {
        using TempDirectory temp = new();
        var authority = new FileAuthorityStateStore(Path.Combine(temp.Path, "authority.json"));
        string transitionPath = Path.Combine(temp.Path, "transition.json");
        var writableTransitions = new FileTransitionStateStore(transitionPath);
        var prepared = AuthorityTransitionRecord.PreparedFrom((await authority.LoadAsync()).Record, "abort-3", 1);
        await writableTransitions.SaveAsync(prepared);
        var transitions = new FileTransitionStateStore(transitionPath, () => true);
        var result = await new AuthorityAbortService(authority, transitions, new InMemoryAuthorityAuditSink())
            .AbortAsync("abort-3", 1);
        Assert.Equal(AbortResultStatus.RecoveryRequired, result.Status);
        Assert.Equal(AuthorityState.RecoveryRequired, (await authority.LoadAsync()).Record.State);
    }

    [Fact]
    public void Rollback_eligibility_requires_all_safety_evidence_and_rejects_target_writes()
    {
        var evaluator = new RollbackEligibilityEvaluator();
        RollbackSafetyEvidence safe = Evidence();
        Assert.Equal(RollbackEligibilityStatus.RollbackEligible, evaluator.Evaluate(safe).Status);
        Assert.Equal(RollbackEligibilityStatus.RollbackNotSafe,
            evaluator.Evaluate(safe with { TargetAuthoritativeWrites = true }).Status);
        Assert.Equal(RollbackEligibilityStatus.RollbackNotSafe,
            evaluator.Evaluate(safe with { Reconciliation = ReconciliationStatus.Mismatched }).Status);
        Assert.Equal(RollbackEligibilityStatus.RecoveryRequired,
            evaluator.Evaluate(safe with { AuthorityEpoch = 2 }).Status);
        Assert.Equal(RollbackEligibilityStatus.NotEvaluated,
            evaluator.Evaluate(safe with { Available = false, Reason = "backup-missing" }).Status);
    }

    [Fact]
    public async Task Rehearsal_rollback_works_only_for_eligible_isolated_context()
    {
        using TempDirectory temp = new();
        var authority = new FileAuthorityStateStore(Path.Combine(temp.Path, "authority.json"));
        var transitions = new FileTransitionStateStore(Path.Combine(temp.Path, "transition.json"));
        var prepared = await new AuthorityCommitService(authority, transitions).PrepareAsync("rollback-1");
        var service = new RehearsalRollbackService(authority, transitions, new InMemoryAuthorityAuditSink());
        var production = await service.RollbackAsync(RehearsalContext.Production, "rollback-1", prepared.Generation, Evidence(prepared.Generation));
        Assert.Equal(RehearsalRollbackStatus.Rejected, production.Status);
        var result = await service.RollbackAsync(RehearsalContext.Qualification, "rollback-1", prepared.Generation, Evidence(prepared.Generation));
        Assert.Equal(RehearsalRollbackStatus.Succeeded, result.Status);
        Assert.True(result.Authority.LegacyAuthoritative);
        Assert.False(result.Authority.TargetRoutingEnabled);
    }

    [Fact]
    public void Recovery_message_is_operator_safe_and_does_not_include_secrets()
    {
        string message = RecoveryOperatorMessage.Persian("CommitInProgress", ["authority-transition-mismatch"]);
        Assert.Contains("بازیابی", message);
        Assert.Contains("CommitInProgress", message);
        Assert.DoesNotContain("password", message, StringComparison.OrdinalIgnoreCase);
    }

    private static RollbackSafetyEvidence Evidence(long generation = 1) => new(
        true, false, false, ReconciliationStatus.Matched, false, false, true, true, true,
        AuthorityState.TargetAuthoritative, generation, generation);

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory() { Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rah-d3-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Path); }
        public string Path { get; }
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); }
    }
}
