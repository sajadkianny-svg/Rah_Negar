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
}
